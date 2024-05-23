using System.Text;
using Blockcore.Features.RPC;
using Blockcore.NBitcoin;
using System.Collections.Concurrent;
using System.Diagnostics;
using Blockcore.Utilities.Extensions;
using Microsoft.EntityFrameworkCore;
using EFCore.BulkExtensions;
using LevelDB;
using System.Reflection.Metadata;
using IndexerORM;



public class Indexer
{
    int HashCacheSize { get; set; }
    int BlockCacheSize { get; set; }
    int BatchCacheSize { get; set; }
    int BatchSizeLimit { get; set; }
    int BlockLimit { get; set; }
    int ParallelInsertCount { get; set; }
    int blockCounter = 0;
    bool preSyncComplete = false;
    RPCClient client;
    // DbContext db;
    BlockingCollection<uint256> HashCache;
    BlockingCollection<Block> BlockCache;
    BlockingCollection<Batch> BatchCache;

    public struct IndexerConfig
    {
        public int HashCacheSize { get; set; }
        public int BlockCacheSize { get; set; }
        public int BatchCacheSize { get; set; }
        public int BatchSizeLimit { get; set; }
        public int BlockLimit { get; set; }
        public int ParallelInsertCount { get; set; }
        public IndexerConfig(int hashCacheSize, int blockCacheSize, int batchCacheSize, int batchSizeLimit, int blockLimit, int parallelInsertCount) : this()
        {
            HashCacheSize = hashCacheSize;
            BlockCacheSize = blockCacheSize;
            BatchCacheSize = batchCacheSize;
            BatchSizeLimit = batchSizeLimit;
            BlockLimit = blockLimit;
            ParallelInsertCount = parallelInsertCount;
        }
    }

    public Indexer(RPCClient Client, IndexerConfig config /*, DbContext DbContext*/)
    {
        HashCacheSize = config.HashCacheSize;
        BlockCacheSize = config.BlockCacheSize;
        BatchCacheSize = config.BatchCacheSize;
        BatchSizeLimit = config.BatchSizeLimit;
        BlockLimit = config.BlockLimit;
        ParallelInsertCount = config.ParallelInsertCount;
        client = Client;
        HashCache = new BlockingCollection<uint256>(HashCacheSize);
        BlockCache = new BlockingCollection<Block>(BlockCacheSize);
        BatchCache = new BlockingCollection<Batch>(BatchCacheSize);
    }
    public async Task RunIndexer(long initIndex)
    {
        Console.WriteLine("Indexer Starting...");
        Stopwatch watch_main = new();
        watch_main.Start();

        List<Task> tasks = [];

        tasks.Add(Task.Run(() => CacheFiller(initIndex)));
        tasks.Add(Task.Run(() => BlockPuller()));
        tasks.Add(Task.Run(() => BatchMaker()));

        List<DbContext> db = [];
        for (int i = 0; i < ParallelInsertCount; i++)
        {
            DbContext context = new AppDbContext();
            context.Database.SetCommandTimeout(200);
            db.Add(context);
        }   
        foreach(var context in db){
            tasks.Add(Task.Run(() => PushBatchToPostgres(context)));
        }
        await Task.WhenAll(tasks);
        watch_main.Stop();
        Console.WriteLine("Time taken ==> " + watch_main.Elapsed + "s"); ;
    }
    public async Task CacheFiller(long initIndex)
    {
        Console.WriteLine("Starting CacheFiller Task");

        int counter = 0;
        while (counter < BlockLimit)
        {
            try
            {
                List<Task<uint256>> TaskList = [];
                for (int i = 0; i < 10; i++)
                {
                    TaskList.Add(client.GetBlockHashAsync((int)initIndex + counter));
                    counter++;
                }
                Stopwatch sw = new();
                sw.Start();
                await Task.WhenAll(TaskList.ToArray());
                sw.Stop();
                foreach (var task in TaskList)
                {
                    HashCache.Add(task.Result);
                }
                if (counter % 5000 == 0)
                {
                    Console.WriteLine(counter);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Problem in downloading.." + ex.Message);
                counter -= 10;
                Console.WriteLine($"retrying at block height => {initIndex + counter} in 2s..");
                await Task.Delay(2000);
            }
        }
        HashCache.CompleteAdding();
        Console.WriteLine("Pre-sync task completed");
    }

    public async Task BlockPuller()
    {
        //pull the block and push to the BlockCache
        Console.WriteLine("Starting Block Puller Task");

        while (!HashCache.IsCompleted)
        {
            try
            {
                List<Task<Blockcore.Consensus.BlockInfo.Block>> TaskList = [];
                for (int i = 0; i < 10; i++)
                {
                    var hash = HashCache.Take();
                    TaskList.Add(client.GetBlockAsync(hash));
                }
                Stopwatch sw = new();
                sw.Start();
                await Task.WhenAll(TaskList.ToArray());

                foreach (var task in TaskList)
                {
                    BlockCache.Add(BlockOperations.MakeBlock(task.Result));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Problem occured in Pulling Blocks => " + ex.Message);
                //TODO -> Handle the already taken hashes
            }
        }
        BlockCache.CompleteAdding();
        Console.WriteLine("Block puller task completed");
    }

    public async Task BatchMaker()
    {
        Console.WriteLine("Starting BatchMaker");
        while (!BlockCache.IsCompleted)
        {
            try
            {
                Console.WriteLine("Starting new batch");
                Batch batch = new();
                batch.blocks = [];
                long size = 0;
                //batchSizeLimit in MB
                while (size < BatchSizeLimit * 1000000)
                {

                    if (BlockCache.TryTake(out var block, TimeSpan.FromMicroseconds(100)))
                    {
                        batch.blocks.Add(block);
                        size += block.Size;
                    }
                    else if (BlockCache.IsCompleted)
                    {
                        Console.WriteLine("Last batch");
                        break;
                    }
                }
                batch.Size = size;
                // Console.WriteLine("Batchload Completed | Batchsize => " + batch.Size);
                BatchCache.Add(batch);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Problem in batchmaker => " + ex);
                //TODO -> handle the the already taken blocks
            }
        }
        BatchCache.CompleteAdding();
        Console.WriteLine("Batchmaker Task Completed");
    }

    public async Task PushBatchToPostgres(DbContext db)
    {
        Console.WriteLine("Starting push to postgres task");
        while (!BatchCache.IsCompleted)
        {
            Batch batch = BatchCache.Take();
            try
            {   
                Console.WriteLine($"Current - thread {Thread.CurrentThread.ManagedThreadId}");
                Console.WriteLine("Pushing batch to db | size => " + batch.Size + " | Blocks => " + batch.blocks.Count);
                Stopwatch sw = new();
                int countRows = 0;
                sw.Start();
                await Task.WhenAll(Task.Run(async () =>
                {
                    await db.BulkInsertAsync(batch.blocks, options => { options.IncludeGraph = true; });
                    await db.BulkSaveChangesAsync();
                }),
                Task.Run(() =>
                {
                    countRows += batch.blocks.Count;
                    foreach (var block in batch.blocks)
                    {
                        countRows += block.Transactions.Count;
                        foreach (var txn in block.Transactions)
                        {
                            countRows += txn.Inputs.Count + txn.Outputs.Count;
                        }
                    }
                })
                );
                sw.Stop();
                Console.WriteLine($"Current - thread {Thread.CurrentThread.ManagedThreadId}");
                Console.WriteLine("Pushed batch to db | size => " + batch.Size + " | Rows => " + countRows + " | Blocks => " + batch.blocks.Count + " | Time taken => " + sw.Elapsed + "s");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Problem in inserting Batch : " + ex);
                Console.WriteLine("Retrying batch");
                // BatchCache.Add(batch);
            }
        }
        Console.WriteLine("Push to postgres task completed");
    }

    public async Task WriteToOutput(Batch batch)
    {
        try
        {
            StringBuilder sb = new();
            sb.AppendLine("| Blockcount = " + batch.blocks.Count + " | Batchsize = " + batch.Size);
            foreach (var block in batch.blocks)
            {
                sb.AppendLine("| Blockhash -> " + block.BlockHash);
            }
            // foreach (var block in batch.blocks)
            // {
            //     sb.AppendLine(block.PreviousBlockHash);
            //     sb.AppendLine(block.BlockHash);
            //     sb.AppendLine(block.BlockIndex.ToString());
            //     sb.AppendLine("Txn count = " + block.Transactions.Count);
            // }
            await File.AppendAllTextAsync("output.txt", sb.ToString());
        }
        catch (Exception ex)
        {
            Console.WriteLine("Problem in writing => " + ex.Message);
        }
    }
}



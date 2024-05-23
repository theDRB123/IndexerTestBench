using System.Text;
using Blockcore.Features.RPC;
using Blockcore.NBitcoin;
using System.Collections.Concurrent;
using System.Diagnostics;
using Blockcore.Utilities.Extensions;
using Microsoft.EntityFrameworkCore;
using EFCore.BulkExtensions;
using LevelDB;



public class Indexer
{
    public int maxCacheElements { get; set; }
    public int maxBatchSize { get; set; }
    public int maxBatchCount { get; set; }
    public int maxHashBatchSize { get; set; }
    public int blockLimit { get; set; }
    int counter = 0;
    bool preSyncComplete = false;
    RPCClient client;
    DbContext db;
    List<uint256> batchHashList = [];
    BlockingCollection<uint256> hashCache;
    BlockingCollection<Batch> batches;
    List<Task<Blockcore.Consensus.BlockInfo.Block>> blocks;

    public struct IndexerConfig
    {
        public int maxCacheElements;
        public int maxBatchSize;
        public int maxBatchCount;
        public int maxHashBatchSize;
        public int blockLimit;
        public IndexerConfig(int maxCacheElements, int maxBatchSize, int maxBatchCount, int maxHashBatchSize, int blockLimit) : this()
        {
            this.maxCacheElements = maxCacheElements;
            this.maxBatchSize = maxBatchSize;
            this.maxBatchCount = maxBatchCount;
            this.maxHashBatchSize = maxHashBatchSize;
            this.blockLimit = blockLimit;
        }
    }

    public Indexer(RPCClient Client, IndexerConfig config, DbContext DbContext)
    {
        maxCacheElements = config.maxCacheElements;
        maxBatchSize = config.maxBatchSize;
        maxBatchCount = config.maxBatchCount;
        maxHashBatchSize = config.maxHashBatchSize;
        blockLimit = config.blockLimit;
        client = Client;
        db = DbContext;
        hashCache = new BlockingCollection<uint256>(maxCacheElements);
        batches = new BlockingCollection<Batch>(maxBatchCount);
        blocks = new(maxBatchSize);
    }
    public async Task RunIndexer(long initIndex)
    {
        Console.WriteLine("Indexer Starting...");
        Stopwatch watch_main = new();
        watch_main.Start();

        Task cacheFillerTask = Task.Run(() => CacheFiller(initIndex));
        Task batchMakerTask = Task.Run(() => BatchMaker());
        Task pushBatchTask = Task.Run(() => PushBatchToPostgres());

        await Task.WhenAll(cacheFillerTask, batchMakerTask, pushBatchTask);
        watch_main.Stop();
        Console.WriteLine("Time taken ==> " + watch_main.Elapsed + "s"); ;
    }

    public async Task CacheFiller(long initIndex)
    {
        Console.WriteLine("Starting CacheFiller Task");
        while (counter < blockLimit)
        {
            try
            {
                List<Task<uint256>> TaskList = [];
                for (int i = 0; i < maxHashBatchSize; i++)
                {
                    TaskList.Add(client.GetBlockHashAsync((int)initIndex + (int)counter));
                    counter++;
                }
                Stopwatch sw = new();
                sw.Start();
                // Console.WriteLine("Starting blockhash download");
                await Task.WhenAll(TaskList.ToArray());
                // counter += maxHashBatchSize;
                sw.Stop();
                foreach (var task in TaskList)
                {
                    hashCache.Add(task.Result);
                }
                if(counter % maxBatchSize == 0){
                    Console.WriteLine(counter);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Problem in downloading.." + ex.Message);
                Console.WriteLine($"retrying at block height in 1s {initIndex + counter}... ");
                await Task.Delay(1000);
            }
        }
        hashCache.CompleteAdding();
        Console.WriteLine("Pre-sync task completed");
    }
    public async Task BatchMaker()
    {
        Console.WriteLine("Starting BatchMaker");
        while (!hashCache.IsCompleted)
        {
            try
            {
                // Console.WriteLine("Starting Batch");
                for (int i = 0; i < maxBatchSize; i++)
                {
                    batchHashList.Add(hashCache.Take());
                }
                Stopwatch sw = new();
                // blocks.AddRange(batchHashList.Select(hash => client.GetBlockAsync(hash)));
                foreach (var hash in batchHashList)
                {
                    blocks.Add(client.GetBlockAsync(hash));
                }

                sw.Start();
                // Console.WriteLine("Downloading Batch");
                await Task.WhenAll(blocks.ToArray());
                sw.Stop();
                // Console.WriteLine("Batch downloaded : Time taken => " + sw.ElapsedMilliseconds + "ms");
                sw.Start();
                Batch batch = BlockOperations.MakeBatch(blocks);
                batches.Add(batch);
                sw.Stop();
                Console.WriteLine("Batchload Completed | Batchsize => " + batch.Size + " | Time taken => " + sw.ElapsedMilliseconds + "ms");

                batchHashList.Clear();
                blocks.Clear();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Problem in batchmaker => " + ex.Message);
            }
        }
        batches.CompleteAdding();
        Console.WriteLine("Batchmaker Task Completed");
    }
    public async Task PushBatchToPostgres()
    {
        // Console.WriteLine("Starting push to postgres task");
        while (!batches.IsCompleted)
        {
            Batch batch = batches.Take();
            try
            {
                //TODO -> push batch
                // await WriteToOutput(batch);
                // IEnumerable<Task> TaskList = batch.blocks.Select((block) => PushToPostgres(block));
                // await Task.WhenAll(TaskList.ToArray());
                Stopwatch sw = new();
                sw.Start();
                // await db.AddRangeAsync(batch.blocks);
                // await db.SaveChangesAsync();
                await db.BulkInsertAsync(batch.blocks, options => { options.IncludeGraph = true;});
                await db.BulkSaveChangesAsync();
                // await Task.Delay(10);
                sw.Stop();
                Console.WriteLine("Pushed batch to db | size => " + batch.Size +" | Time taken => " + sw.ElapsedMilliseconds + "ms");
            }catch(Exception ex){
                Console.WriteLine("Problem in inserting Batch : " + ex);
                Console.WriteLine("Retrying batch");
                // batches.Add(batch);
            }
        }
        Console.WriteLine("Push to postgres task completed");
    }

    // public async Task PushToPostgres(Block block)
    // {
    //     await db.AddR (block);
    // }

    public async Task WriteToOutput(Batch batch)
    {
        try
        {
            StringBuilder sb = new();
            sb.AppendLine("| Blockcount = " + batch.blocks.Count + " | Batchsize = " + batch.Size);
            foreach(var block in batch.blocks){
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



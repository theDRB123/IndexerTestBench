using System.Text;
using Blockcore.Features.RPC;
using Blockcore.NBitcoin;
using System.Collections.Concurrent;
using System.Diagnostics;


public class Indexer
{
    public int maxCacheElements { get; set; }
    public int maxBatchSize { get; set; }
    public int blockLimit { get; set; }
    int counter;
    RPCClient client;
    List<uint256> batchHashList = [];
    BlockingCollection<uint256> hashCache;
    BlockingCollection<Batch> batches;
    List<Task<Blockcore.Consensus.BlockInfo.Block>> blocks;


    public Indexer(RPCClient Client)
    {
        maxCacheElements = 200;
        maxBatchSize = 10;
        blockLimit = 5000;
        counter = 0;
        client = Client;
        hashCache = new BlockingCollection<uint256>(maxCacheElements);
        batches = new BlockingCollection<Batch>(2);
        blocks = new(maxBatchSize);
    }
    public async Task RunIndexer()
    {
        Console.WriteLine("Indexer Starting...");
        Stopwatch watch_main = new();
        watch_main.Start();
        Task cacheFillerTask = Task.Run(() => CacheFiller(0));
        Task batchMakerTask = Task.Run(() => BatchMaker());
        Task pushBatchTask = Task.Run(() => PushBatchToPostgres());
        // Console.WriteLine("Start pre-sync hash");
        // await Task.WhenAll(cacheFillerTask);
        // Console.WriteLine("Completed pre-sync hash");
        await Task.WhenAll(cacheFillerTask, batchMakerTask, pushBatchTask);
        watch_main.Stop();
        Console.WriteLine("Time taken ==> " + watch_main.Elapsed + "s");
        // await Task.WhenAll(batchMakerTask);
    }

    public async Task CacheFiller(uint initIndex)
    {
        Console.WriteLine("Starting CacheFiller Task");
        while (counter <= blockLimit)
        {
            try
            {
                List<Task<uint256>> TaskList = [];
                Console.WriteLine("Adding batch pre-sync tasks");
                for (int i = 0; i < maxBatchSize; i++)
                {
                    TaskList.Add(client.GetBlockHashAsync((int)initIndex + (int)counter));
                }

                Stopwatch sw = new();
                sw.Start();
                Console.WriteLine("Starting blockhash download");
                await Task.WhenAll(TaskList.ToArray());
                counter += maxBatchSize;
                sw.Stop();
                Console.WriteLine("Time for pre-sync = " + sw.ElapsedMilliseconds + "ms");
                foreach (var task in TaskList)
                {
                    hashCache.Add(task.Result);
                }
                if (counter == maxCacheElements)
                {
                    Console.WriteLine("CacheFilled");
                }
                Console.WriteLine(counter);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Problem in downloading.." + ex.Message);
                Console.WriteLine($"retrying at block height in 1s {initIndex + counter}... ");
                await Task.Delay(1000);
            }
        }
    }
    public async Task BatchMaker()
    {
        Console.WriteLine("Starting BatchMaker");
        while (counter <= blockLimit)
        {
            for (int i = 0; i < maxBatchSize; i++)
            {
                batchHashList.Add(hashCache.Take());
            }
            Stopwatch sw = new();
            // blocks.AddRange(batchHashList.Select(hash => GetBlockTask(hash)));
            foreach (var hash in batchHashList)
            {
                blocks.Add(client.GetBlockAsync(hash));
            }

            sw.Start();
            Console.WriteLine("Downloading Batch");
            await Task.WhenAll(blocks.ToArray());
            sw.Stop();
            Console.WriteLine("Batch downloaded : Time taken => " + sw.ElapsedMilliseconds + "ms");
            sw.Start();
            Batch batch = BlockOperations.MakeBatch(blocks);
            batches.Add(batch);
            sw.Stop();
            Console.WriteLine("Batchload Completed | Batchsize => " + batch.Size + " | Time taken => " + sw.ElapsedMilliseconds + "ms");

            batchHashList.Clear();
            blocks.Clear();
        }
    }
    public async Task PushBatchToPostgres()
    {
        Console.WriteLine("Starting push to postgres task");
        while (counter <= blockLimit)
        {
            Batch batch = batches.Take();
            //TODO -> push batch
            Console.WriteLine("Pushed batch to db");
            //instead write to output file
            await WriteToOutput(batch);
            await Task.Delay(1000);
        }
    }

    public async Task WriteToOutput(Batch batch)
    {
        StringBuilder sb = new();
        sb.AppendLine("Batch");
        sb.AppendLine("Blockcount = " + batch.blocks.Count);
        sb.AppendLine("Blocks");
        foreach (var block in batch.blocks)
        {
            sb.AppendLine(block.PreviousBlockHash);
            sb.AppendLine(block.BlockHash);
            sb.AppendLine(block.BlockIndex.ToString());
            sb.AppendLine("Txn count = " + block.Transactions.Count);
        }
        await File.AppendAllTextAsync("output.txt", sb.ToString());
    }
}



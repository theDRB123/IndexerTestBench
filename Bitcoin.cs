//add the get block from block height functions
using BenchmarkDotNet.Engines;
using BitcoinLib.Services.Coins.Base;
using BitcoinLib.Services.Coins.Bitcoin;
using BitcoinLib.Responses;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using BitcoinLib.Responses.SharedComponents;
using System.Configuration;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json.Linq;

using Blockcore;
using Blockcore.Features.RPC;
using Blockcore.Networks;
using Blockcore.Consensus.BlockInfo;
using Blockcore.NBitcoin;
using Blockcore.Features.Consensus.ProvenBlockHeaders;
using Polly.NoOp;
using Blockcore.Consensus.TransactionInfo;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;


// public class IndexerBitcoin
// {
//     private readonly ICoinService coinService;

//     public IndexerBitcoin()
//     {
//         coinService = new BitcoinService(useTestnet: true);
//         Console.Write("\n\nConnecting to {0} {1}Net via RPC at {2}...", coinService.Parameters.CoinLongName, (coinService.Parameters.UseTestnet ? "Test" : "Main"), coinService.Parameters.SelectedDaemonUrl);

//         var blockHash = FetchBlockByHeight(276000);
//         Block block = FetchBlockByHash(blockHash).Item1;
//     }

//     public string FetchBlockByHeight(long blockheight)
//     {
//         var blockResposnse = coinService.GetBlockHash(blockheight);
//         return blockResposnse;
//     }

//     public (Block, string) FetchBlockByHash(string blockHash)
//     {
//         var block = coinService.GetBlock(blockHash, verbosity: 2);
//         string nextBlockHash = block.NextBlockHash;
//         Block output = MakeBlockFromResponse(block);
//         return (output, nextBlockHash);
//     }

//     //return a list of blocks, transaction, inputs and outputs which will be given to an insert function to insert into postgres in a single batch
//     public (Batch, string) makeBatch(string blockHash)
//     {
//         Batch batch = new();
//         //go through each block from the given blockHash, add this the batch size is exceeded
//         //then return the next block
//         var client = new RPCClient(new NetworkCredential())
//     }

//     public Block MakeBlockFromResponse(GetBlockResponseVerbose response)
//     {
//         Block block = new();
//         //add the block data
//         block.BlockHash = response.Hash;
//         block.BlockIndex = response.Height;
//         block.Version = response.Version;
//         block.Merkleroot = response.MerkleRoot;
//         block.Bits = response.Bits;
//         block.PreviousBlockHash = response.PreviousBlockHash;
//         block.Nonce = response.Nonce;
//         block.BlockTime = response.Time;
//         block.Transactions = [];
//         //add transactions
//         foreach (var txn in response.Tx)
//         {
//             Transaction transaction = new();
//             //setup the transaction, add inputs and outputs
//             transaction.BlockHash = block.BlockHash;
//             transaction.RawTransaction = txn.Hex;
//             transaction.TXID = txn.TxId;
//             transaction.Inputs = new List<Input>();
//             transaction.Outputs = new List<Output>();

//             int index = 0;
//             foreach (var vin in txn.Vin)
//             {
//                 Input input = new()
//                 {
//                     OutpointTXID = vin.TxId,
//                     OutpointVOUT = vin.Vout,
//                     TXID = transaction.TXID,
//                     VOUT = index.ToString(),
//                     //TODO Fetch the value from previous output
//                     Value = 0,
//                     ScriptSig = null
//                 };
//                 transaction.Inputs.Add(input);
//                 index++;
//             }
//             index = 0;
//             foreach (var vout in txn.Vout)
//             {
//                 Output output = new()
//                 {
//                     TXID = transaction.TXID,
//                     VOUT = index.ToString(),
//                     Value = (long)vout.Value,
//                     ScriptPubKeyHex = vout.ScriptPubKey.Hex
//                 };
//                 transaction.Outputs.Add(output);
//                 index++;
//             }

//             block.Transactions.Add(transaction);
//         }
//         return block;
//     }

// }


//Processing
public class Indexer
{
    //Task -> fetch block header, 
    //Task -> use the prefetched window -> pull complete blocks to create a batch
    //Task -> process the batch and convert it to the models
    //Task -> push the models to postgres
    public int maxCacheElements { get; set; }
    public int maxBatchSize { get; set; }
    public int blockLimit { get; set;}
    RPCClient client;
    List<uint256> batchHashList = [];
    BlockingCollection<uint256> hashCache;
    BlockingCollection<List<Block>> batches;
    List<Task<Blockcore.Consensus.BlockInfo.Block>> blocks;

    
    public Indexer(RPCClient Client)
    {
        maxCacheElements = 10;
        maxBatchSize = 5;
        blockLimit = 100;
        client = Client;
        hashCache = new BlockingCollection<uint256>(maxCacheElements);
        batches = new BlockingCollection<List<Block>>(2);
        blocks = new(maxBatchSize);
    }
    public async Task RunIndexer(){
        Task cacheFillerTask = Task.Run(() => CacheFiller(1));
        // await Task.WhenAll(cacheFillerTask);
        Task batchMakerTask = Task.Run(() => BatchMaker());
        Task pushBatchTask = Task.Run(() => PushBatchToPostgres());
        await Task.WhenAll(cacheFillerTask , batchMakerTask, pushBatchTask);
    }

    public async Task CacheFiller(uint initIndex)
    {
        uint counter = 0;
        Console.WriteLine("Starting CacheFiller Task");
        while (true)
        {
            try
            {
                uint256 hash = await client.GetBlockHashAsync((int)initIndex + (int)counter);
                //blocks till hashCache emptied
                hashCache.Add(hash);
                if(counter == maxBatchSize){
                    Console.WriteLine("Batch pre-sync done on thread: " + Thread.CurrentThread.Name);
                }
                if(counter == maxCacheElements){
                    Console.WriteLine("CacheFilled on thread: " + Thread.CurrentThread.Name);
                }
                if(counter >= blockLimit){
                    break;
                }
                counter++;
                Console.WriteLine(counter);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Console.WriteLine($"retrying at block height in 1s {initIndex + counter}... ");
                await Task.Delay(1000);
            }
        }
    }
    public async Task BatchMaker()
    {
        Console.WriteLine("Starting BatchMaker");
        while (true)
        {
            //create multiple async task for each block and waitAll
            //add to batchHashList
            //block if not will above
            for (int i = 0; i < maxBatchSize; i++){
                batchHashList.Add(hashCache.Take());
            }
            Console.WriteLine("Blocks added");

            // blocks.AddRange(batchHashList.Select(hash => GetBlockTask(hash)));
            foreach(var hash in batchHashList){
                blocks.Add(client.GetBlockAsync(hash));
            }
            //now after adding the block tasks we wait for all the tasks to complete

            Console.WriteLine("Loading Blocks");
            await Task.WhenAll(blocks.ToArray());
            Console.WriteLine("Blocks Loaded");
            //again start the cache filler

            //now make the results into a batch with custom model...
            List<Block> batch = MakeBatch(blocks);
            // List<Block> batch = [];
            Console.WriteLine($"Batchsize -> {batch.Count}");
            batches.Add(batch);
            batchHashList.Clear();
            blocks.Clear();
        }
    }
    public async Task PushBatchToPostgres()
    {
        Console.WriteLine("Starting push to postgres task");
        while (true)
        {
            List<Block> batch = batches.Take();
            //TODO -> push batch
            Console.WriteLine("Pushed batch to db");
            //instead write to output file
            await WriteToOutput(batch);
            await Task.Delay(1000);
        }
    }

    public async Task WriteToOutput(List<Block> batch){
        StringBuilder sb = new();
        sb.AppendLine("Batch");
        sb.AppendLine("Blockcount = " + batch.Count);
        foreach (var block in batch){
            sb.AppendLine("block");
            sb.AppendLine(block.PreviousBlockHash);
            sb.AppendLine(block.BlockHash);
            sb.AppendLine(block.BlockIndex.ToString());
            sb.AppendLine("Txn count = " + block.Transactions.Count);
        }
        // await File.WriteAllTextAsync("output.txt", sb.ToString());
        await File.AppendAllTextAsync("output.txt", sb.ToString());
    }

    public List<Block> MakeBatch(List<Task<Blockcore.Consensus.BlockInfo.Block>> blocks)
    {
        List<Block> output = [];
        foreach (var block in blocks)
        {
            output.Add(MakeBlock(block.Result));
        }
        return output;
    }

    public Block MakeBlock(Blockcore.Consensus.BlockInfo.Block block)
    {
        Block output = new();
        BlockHeader bh = block.Header;
        output.Version = bh.Version;
        output.Merkleroot = bh.HashMerkleRoot.ToString();
        output.Nonce = bh.Nonce.ToString();
        output.PreviousBlockHash = bh.HashPrevBlock.ToString();
        output.Bits = bh.Bits.ToString();
        output.BlockHash = bh.GetHash().ToString();
        //this has to be dealt with somehow :(
        output.BlockIndex = 0;
        output.BlockTime = bh.Time;
        output.Transactions = [];

        //transactions
        foreach (var txn in block.Transactions)
        {
            output.Transactions.Add(MapTransaction(txn, output.BlockHash));
        }
        return output;
    }
    public Transaction MapTransaction(Blockcore.Consensus.TransactionInfo.Transaction transaction, string BlockHash)
    {
        Transaction txn = new();
        //TODO -> fix the rawtransaction issue
        txn.RawTransaction = transaction.ToHex();
        txn.BlockHash = BlockHash;
        txn.TXID = transaction.GetHash().ToString();
        //give the inputs
        txn.Inputs = [];
        txn.Outputs = [];
        uint index = 0;
        
        foreach (var Input in transaction.Inputs)
        {
            Input input = new();
            // Blockcore.Consensus.TransactionInfo.OutPoint
            input.TXID = txn.TXID;
            input.VOUT = index;
            input.OutpointTXID = Input.PrevOut.Hash.ToString();
            input.OutpointVOUT = Input.PrevOut.N;
            input.ScriptSig = Input.ScriptSig.ToHex();
            //TODO -> The value needs to be derived from the prevout
            input.Value = 0;
            txn.Inputs.Add(input);
            index++;
        }
        index = 0;
        foreach (var Output in transaction.Outputs)
        {
            Output output = new();
            output.TXID = txn.TXID;
            output.VOUT = index;
            output.ScriptPubKeyHex = Output.ScriptPubKey.ToHex();
            //TODO -> The address needs to be derived
            output.Address = " ";
            output.Value = Output.Value;
            txn.Outputs.Add(output);
            index++;
        }
        return txn;
    }
}
//RPC client
public static class BitcoinMethods
{
    public static RPCClient GetBitcoinClient()
    {
        string rpcURL = "http://4.247.157.198:18332";
        string rpcUser = "drb";
        string rpcPassword = "drb";

        Blockcore.Networks.Network network = new Blockcore.Networks.Bitcoin.BitcoinTest();

        RPCCredentialString rPCCredentialString = new()
        {
            UserPassword = new NetworkCredential(userName: rpcUser, password: rpcPassword)
        };

        return new RPCClient(rPCCredentialString.ToString(), new Uri(rpcURL), network: network);
    }
};

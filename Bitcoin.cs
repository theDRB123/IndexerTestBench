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
    public uint maxCacheElements { get; set; }
    public uint maxBatchSize { get; set; }
    RPCClient client;
    List<uint256> batchHashList = [];
    List<uint256> hashCache = [];
    List<Task<Blockcore.Consensus.BlockInfo.Block>> blocks;

    public Indexer(RPCClient Client)
    {
        maxCacheElements = 200;
        client = Client;
    }

    public async Task CacheFiller(uint initIndex)
    {
        uint counter = 0;
        //starting from the initIndex, start filling the block headers
        while (hashCache.Count < maxCacheElements)
        {
            //make a request to RPC
            try
            {
                uint256 hash = await client.GetBlockHashAsync((int)initIndex + (int)counter);
                hashCache.Add(hash);
                counter++;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Console.WriteLine($"retrying at block height {initIndex + counter}... ");
            }
        }
        Console.WriteLine("CacheFilled");
    }

    public async Task BatchMaker()
    {
        //create multiple async task for each block and waitAll
        //add to batchHashList
        batchHashList.AddRange(hashCache[0..(int)maxBatchSize]);
        foreach (uint256 hash in batchHashList)
        {
            blocks.Add(GetBlockTask(hash));
        }

        //now after adding the block tasks we wait for all the tasks to complete

        Task.WaitAll(blocks.ToArray());
        

    }

    public async Task<Blockcore.Consensus.BlockInfo.Block> GetBlockTask(uint256 blockHash)
    {
        var block = await client.GetBlockAsync(blockHash);
        return block;
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

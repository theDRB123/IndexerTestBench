using System;
using System.Linq;

using BenchmarkDotNet.Attributes;
using Blockcore.Features.RPC;
using Blockcore.NBitcoin;

namespace IndexerORM
{
    public class IndexerEFcore
    {
        public async Task TestEFCore(RPCClient client)
        {
            Console.WriteLine("EFCore Test");
            using var db = new AppDbContext();
            Console.WriteLine("Connected EFcore");

            string hash = "000000000000012d66196244b4cc93f955c64ee613fee8b7626825b72055994b";
            // uint256 hashInt = 0000000000000000000000000000000000000000000000000000000100101101011001100001100101100010010001001011010011001100100100111111100101010101110001100100111011100110000100111111111011101000101101110110001001101000001001011011011100100000010101011001100101001011;
            //fetch and add block
            Blockcore.Consensus.BlockInfo.Block block = await client.GetBlockAsync(uint256.Parse(hash));
            Block blockToPush = BlockOperations.MakeBlock(block);

            Console.WriteLine("Inserting a new Block");
            await db.AddAsync(blockToPush);
            await db.SaveChangesAsync();
            Console.WriteLine("Block Added");
        }

    }
}
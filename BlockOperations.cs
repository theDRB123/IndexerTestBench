//add the get block from block height functions
using Blockcore.Consensus.BlockInfo;
using Blockcore.Utilities.Extensions;


public class Batch{
    public List<Block> blocks;
    public long Size { get; set; }
}



public static class BlockOperations
{
    public static Batch MakeBatch(List<Task<Blockcore.Consensus.BlockInfo.Block>> blocks)
    {
        Batch output = new();
        output.blocks = [];
        foreach (var block in blocks)
        {
            output.blocks.Add(MakeBlock(block.Result));
            output.Size += block.Result.BlockSize.Value;
        }
        return output;
    }
    public static Block MakeBlock(Blockcore.Consensus.BlockInfo.Block block)
    {
        Block output = new();
        BlockHeader bh = block.Header;
        output.Size = block.BlockSize.Value;
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
    public static Transaction MapTransaction(Blockcore.Consensus.TransactionInfo.Transaction transaction, string BlockHash)
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




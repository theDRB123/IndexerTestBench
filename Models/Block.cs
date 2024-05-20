using System.ComponentModel.DataAnnotations;

public class Block
{
    //primary key
    [Key]
    public string BlockHash { get; set; }
    public long BlockIndex { get; set; } //height
    public long BlockTime { get; set; } //timestamp
    //this will be a foreign key referenceing previous block
    public string PreviousBlockHash { get; set; }
    public string Bits { get; set; }
    public string Merkleroot { get; set; }
    public string Nonce { get; set; }
    public long Version { get; set; }
    public ICollection<Transaction> Transactions { get; set; }
}
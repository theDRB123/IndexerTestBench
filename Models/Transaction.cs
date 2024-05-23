using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Transaction
{
    //Each Block will have reference to a list of transactions
    [Key]
    public string TXID { get; set; }
    public string RawTransaction { get; set; }
    //blockID will be a foreign key referencing the block table
    [ForeignKey("Block")]
    public string BlockHash { get; set; }
    public virtual Block Block { get; set; }
    public ICollection<Input> Inputs { get; set; }
    public ICollection<Output> Outputs { get; set; }
}
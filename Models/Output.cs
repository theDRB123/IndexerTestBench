using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Output
{
    [Key]
    [ForeignKey("Transaction")]
    public string TXID { get; set; }
    [Key]
    public uint VOUT { get; set; }
    public string Address { get; set; }
    public string ScriptPubKeyHex { get; set; }
    public long Value { get; set; }
    public virtual Transaction Transaction { get; set; }
}
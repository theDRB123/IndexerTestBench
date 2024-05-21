
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Input
{
    [Key]
    public string TXID { get; set; }
    public uint VOUT { get; set; }
    public virtual Transaction Transaction { get; set; }
    public string OutpointTXID { get; set; }
    public uint OutpointVOUT { get; set; }
    public string ScriptSig { get; set; }
    public long Value { get; set; }
    public virtual Transaction OutpointTransaction { get; set; }
}


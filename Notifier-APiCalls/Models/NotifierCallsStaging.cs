using System.ComponentModel.DataAnnotations.Schema;

namespace NotifierAPI.Models;

[Table("NotifierCalls_Staging", Schema = "dbo")]
public class NotifierCallsStaging
{
    public long Id { get; set; }
    
    [Column("DateAndTime")]
    public DateTime DateAndTime { get; set; }
    
    [Column("PhoneNumber")]
    public string PhoneNumber { get; set; } = string.Empty;
    
    [Column("StatusText")]
    public string? StatusText { get; set; }
    
    [Column("SourceFile")]
    public string? SourceFile { get; set; }
    
    [Column("LoadedAt")]
    public DateTime? LoadedAt { get; set; }
}

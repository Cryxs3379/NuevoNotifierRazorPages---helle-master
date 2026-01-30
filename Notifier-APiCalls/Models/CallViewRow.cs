using System.ComponentModel.DataAnnotations.Schema;

namespace NotifierAPI.Models;

[Table("vw_Outgoing_24h_ConCliente", Schema = "dbo")]
public class Outgoing24hRow
{
    [Column("Id")]
    public long Id { get; set; }

    [Column("DateAndTime")]
    public DateTime DateAndTime { get; set; }

    [Column("PhoneNumber")]
    public string PhoneNumber { get; set; } = "";

    [Column("NombreCompleto")]
    public string? NombreCompleto { get; set; }

    [Column("NombrePila")]
    public string? NombrePila { get; set; }
}

[Table("vw_Incoming_NoAtendidas_24h_ConCliente", Schema = "dbo")]
public class IncomingNoAtendidas24hRow
{
    [Column("Id")]
    public long Id { get; set; }

    [Column("DateAndTime")]
    public DateTime DateAndTime { get; set; }

    [Column("PhoneNumber")]
    public string PhoneNumber { get; set; } = "";

    [Column("NombreCompleto")]
    public string? NombreCompleto { get; set; }

    [Column("NombrePila")]
    public string? NombrePila { get; set; }

    [Column("Recall")]
    public long? Recall { get; set; }
}

[Table("vw_Incoming_Atendidas_24h_ConCliente", Schema = "dbo")]
public class IncomingAtendidas24hRow
{
    [Column("Id")]
    public long Id { get; set; }

    [Column("DateAndTime")]
    public DateTime DateAndTime { get; set; }

    [Column("PhoneNumber")]
    public string PhoneNumber { get; set; } = "";

    [Column("NombreCompleto")]
    public string? NombreCompleto { get; set; }

    [Column("NombrePila")]
    public string? NombrePila { get; set; }
}

using System.ComponentModel.DataAnnotations.Schema;

namespace NotifierAPI.Models
{
    [Table("vw_MissedCalls_WithClientName", Schema = "dbo")]
    public class MissedCallWithClientNameRow
    {
        [Column("Id")]
        public long Id { get; set; }

        [Column("DateAndTime")]
        public DateTime DateAndTime { get; set; }

        [Column("PhoneNumber")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Column("NombrePila")]
        public string? NombrePila { get; set; }

        [Column("NombreCompleto")]
        public string? NombreCompleto { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NotifierAPI.Models
{
    [Table("NotifierIncomingCalls", Schema = "dbo")]
    public class IncomingCall
    {
        [Key]
        public long Id { get; set; }

        [Column("DateAndTime")]
        public DateTime DateAndTime { get; set; }

        [Column("PhoneNumber")]
        [MaxLength(50)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Column("Status")]
        public byte Status { get; set; }

        [Column("ClientCalledAgain")]
        public long? ClientCalledAgain { get; set; }

        [Column("Recall")]
        public bool? Recall { get; set; }

        [Column("RecalledAt")]
        public DateTime? RecalledAt { get; set; }

        [Column("RecalledByOutgoingId")]
        public long? RecalledByOutgoingId { get; set; }
    }
}

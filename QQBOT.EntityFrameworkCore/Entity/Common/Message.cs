using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QQBOT.EntityFrameworkCore.Entity.Common
{
    [Table("Message")]
    public class QMessage
    {
        [Key]
        public string MessageId { get; set; }

        public string Message { get; set; }
        
        // 懒得建enum了
        public string Type { get; set; }
    }
}
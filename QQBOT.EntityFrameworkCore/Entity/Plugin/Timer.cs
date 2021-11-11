using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QQBOT.EntityFrameworkCore.Entity.Plugin
{
    [Table("Timer")]
    public class Timer
    {
        [Key]
        public long Id { get; set; }
        public DateTime TimeBegin { get; set; }
        public DateTime? TimeEnd { get; set; }
        public long Uid { get; set; }
        public string Name { get; set; }
    }
}
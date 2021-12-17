using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QQBOT.EntityFrameworkCore.Entity.Plugin.MaiMaiDx
{
    [Table("MaiMaiDx.Guess")]
    [Index(nameof(UId))]
    public class MaiMaiDxGuess
    {
        [Key] public long Id { get; set; }
        public long UId { get; set; }
        public string Name { get; set; }
        public int TimesStart { get; set; }
        public int TimesCorrect { get; set; }
        public int TimesWrong { get; set; }

        public MaiMaiDxGuess()
        {
        }

        public MaiMaiDxGuess(long uid, string name)
        {
            UId          = uid;
            Name         = name;
            TimesStart   = 0;
            TimesCorrect = 0;
            TimesWrong   = 0;
        }
    }
}
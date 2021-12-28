using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using QQBot.EntityFrameworkCore.Entity.Plugin.Shared;

namespace QQBot.EntityFrameworkCore.Entity.Plugin.MaiMaiDx;

[Table("MaiMaiDx.Guess")]
[Index(nameof(UId))]
public class MaiMaiDxGuess : SongGuess
{
    [Key] public long Id { get; set; }

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
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

    public MaiMaiDxGuess(long uid, string name) : base(uid, name)
    {
    }
}
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Marisa.EntityFrameworkCore.Entity.Plugin.Shared;
using Microsoft.EntityFrameworkCore;

namespace Marisa.EntityFrameworkCore.Entity.Plugin.Chunithm;

[Table("Chunithm.Guess")]
[Index(nameof(UId))]
public class ChunithmGuess : SongGuess
{
    [Key] public long Id { get; set; }

    public ChunithmGuess()
    {
    }

    public ChunithmGuess(long uid, string name) : base(uid, name)
    {
    }
}
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Marisa.EntityFrameworkCore.Entity.Plugin.Shared;

namespace Marisa.EntityFrameworkCore.Entity.Plugin.Chunithm;

[Table("Chunithm.Guess")]
public class ChunithmGuess : SongGuess
{
    public ChunithmGuess()
    {
    }

    public ChunithmGuess(long uid, string name) : base(uid, name)
    {
    }

    [Key] public long Id { get; set; }
}
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Marisa.EntityFrameworkCore.Entity.Plugin.Shared;
using Microsoft.EntityFrameworkCore;

namespace Marisa.EntityFrameworkCore.Entity.Plugin.Arcaea;

[Table("Arcaea.Guess")]
[Index(nameof(UId))]
public class ArcaeaGuess : SongGuess
{
    [Key] public long Id { get; set; }

    public ArcaeaGuess()
    {
    }

    public ArcaeaGuess(long uid, string name) : base(uid, name)
    {
    }
}
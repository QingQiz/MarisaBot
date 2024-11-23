using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Marisa.EntityFrameworkCore.Entity.Plugin.Shared;

namespace Marisa.EntityFrameworkCore.Entity.Plugin.Arcaea;

[Table("Arcaea.Guess")]
public class ArcaeaGuess : SongGuess
{
    public ArcaeaGuess()
    {
    }

    public ArcaeaGuess(long uid, string name) : base(uid, name)
    {
    }

    [Key] public long Id { get; set; }
}
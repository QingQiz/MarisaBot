using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Marisa.EntityFrameworkCore.Entity.Plugin.Shared;
using Microsoft.EntityFrameworkCore;

namespace Marisa.EntityFrameworkCore.Entity.Plugin.Ongeki;

[Table("Ongeki.Guess")]
public class OngekiGuess : SongGuess
{
    [Key]
    public long Id { get; set; }

    public OngekiGuess() {}

    public OngekiGuess(long uid, string name) : base(uid, name) {}
}
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Marisa.EntityFrameworkCore.Entity.Plugin.Osu;

[Table("Osu.Bind")]
[Index(nameof(UserId))]
[Index(nameof(OsuUserId))]
public class OsuBind
{
    [Key]
    public long Id { get; set; }
    public long UserId { get; set; }
    
    public string OsuUserName { get; set; }

    public long OsuUserId { get; set; }

    public string GameMode { get; set; }
}
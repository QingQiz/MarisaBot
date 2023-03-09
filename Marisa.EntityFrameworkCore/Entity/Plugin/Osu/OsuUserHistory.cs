using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Marisa.EntityFrameworkCore.Entity.Plugin.Osu;

[Table("Osu.UserHistory")]
[Index(nameof(OsuUserId))]
[Index(nameof(OsuUserName))]
public class OsuUserHistory
{
    [Key]
    public long Id { get; set; }
    
    public int Mode { get; set; }

    public string OsuUserName { get; set; }

    public long OsuUserId { get; set; }
    
    public string UserInfo { get; set; }
}
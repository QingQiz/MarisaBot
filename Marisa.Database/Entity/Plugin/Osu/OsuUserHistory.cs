using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Realms;

namespace Marisa.Database.Entity.Plugin.Osu;

[Table("Osu.UserHistory")]
public partial class OsuUserHistory : IRealmObject
{
    [Key]
    [PrimaryKey]
    public long Id { get; set; }
    
    public int Mode { get; set; }

    [Indexed]
    public string OsuUserName { get; set; } = string.Empty;

    [Indexed]
    public long OsuUserId { get; set; }
    
    public string UserInfo { get; set; } = string.Empty;

    public DateTimeOffset CreationTime { get; set; }
}

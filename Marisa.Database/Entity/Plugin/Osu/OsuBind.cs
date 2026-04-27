using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Realms;

namespace Marisa.Database.Entity.Plugin.Osu;

[Table("Osu.Bind")]
public partial class OsuBind : IRealmObject
{
    [Key]
    [PrimaryKey]
    public long Id { get; set; }

    [Indexed]
    public long UserId { get; set; }
    
    public string OsuUserName { get; set; } = string.Empty;

    [Indexed]
    public long OsuUserId { get; set; }

    public string GameMode { get; set; } = string.Empty;
}

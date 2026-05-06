using Marisa.Database.Entity;
using Realms;

namespace Marisa.Database.Entity.Plugin.Osu;

public partial class OsuBind : IRealmObject, IHaveId
{
    [PrimaryKey]
    public long Id { get; set; }

    [Indexed]
    public long UserId { get; set; }
    
    public string OsuUserName { get; set; } = string.Empty;

    [Indexed]
    public long OsuUserId { get; set; }

    public string GameMode { get; set; } = string.Empty;
}

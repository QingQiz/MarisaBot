using System;
using Marisa.Database.Entity;
using Realms;

namespace Marisa.Database.Entity.Plugin.Osu;

public partial class OsuUserHistory : IRealmObject, IHaveId
{
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

using System;
using Realms;

namespace Marisa.Database.Entity.Plugin.DivingFish;

/// <summary>
///     水鱼 OAuth 授权与票据。一行对应一个 QQ 在一个游戏上的授权，
///     与游戏绑定（ServerName）解耦：切换绑定不影响已获得的授权。
/// </summary>
public partial class DivingFishAuthToken : IRealmObject, IHaveId
{
    [PrimaryKey]
    public long Id { get; set; }

    [Indexed]
    public long Qq { get; set; }

    public string Game { get; set; } = string.Empty;

    /// <summary>设备码授权返回的用户标识，拉票时作为 OBO subject 使用。</summary>
    public string Sub { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;

    public string AccessToken { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

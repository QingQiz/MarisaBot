using System;
using Realms;

namespace Marisa.Database.Entity.Plugin.Lxns;

/// <summary>
///     落雪 OAuth 授权与票据。一行对应一个 QQ（令牌是账号级的，与游戏无关），
///     与游戏绑定解耦：切换绑定不影响已获得的授权。
/// </summary>
public partial class LxnsAuthToken : IRealmObject, IHaveId
{
    [PrimaryKey]
    public long Id { get; set; }

    [Indexed]
    public long Qq { get; set; }

    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

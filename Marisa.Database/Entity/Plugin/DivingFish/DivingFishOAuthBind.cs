using System;
using Marisa.Database.Entity;
using Realms;

namespace Marisa.Database.Entity.Plugin.DivingFish;

/// <summary>
///     水鱼 OAuth 绑定：QQ → 水鱼 sub（用户 ID）。
///     绑定流程（授权码 + PKCE + 一次性证明确认）完成后写入正式绑定。
/// </summary>
public partial class DivingFishOAuthBind : IRealmObject, IHaveId
{
    [PrimaryKey]
    public long Id { get; set; }

    /// <summary>QQ 号（应用侧用户标识，唯一）</summary>
    [Indexed]
    public long Qq { get; set; }

    /// <summary>水鱼用户 ID（唯一）</summary>
    [Indexed]
    public string Sub { get; set; } = "";

    /// <summary>水鱼用户名（绑定时获取，用于展示）</summary>
    public string Username { get; set; } = "";

    /// <summary>已授予的 scope（空格分隔）</summary>
    public string Scopes { get; set; } = "";

    /// <summary>授权码流程换取到的 refresh token（30 天，强制轮换，每次刷新后更新）</summary>
    public string RefreshToken { get; set; } = "";

    /// <summary>状态：verified / unverified</summary>
    public string Status { get; set; } = "verified";

    /// <summary>绑定确认时间</summary>
    public DateTimeOffset VerifiedAt { get; set; }
}

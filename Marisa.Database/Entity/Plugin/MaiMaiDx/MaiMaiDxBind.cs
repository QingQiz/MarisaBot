#nullable enable
using Marisa.Database.Entity;
using Realms;

namespace Marisa.Database.Entity.Plugin.MaiMaiDx;

public partial class MaiMaiDxBind : IRealmObject, IHaveId, IHaveUId
{
    public MaiMaiDxBind() {}

    public MaiMaiDxBind(long uid, int aimeId)
    {
        UId    = uid;
        AimeId = aimeId;
    }

    [PrimaryKey]
    public long Id { get; set; }

    [Indexed]
    public long UId { get; set; }

    public int AimeId { get; set; }

    // 注解必须保持可空：optional→required 的 Realm 自动迁移是删列重建，会清空存量数据
    public string? ServerName { get; set; } = string.Empty;

    /// <summary>
    ///     maimai DX 好友码（游戏内「フレンド」里的数字）。用于 `导` 命令经 maimai-score-hub 推分。
    ///     仅存好友码（非机密），查分器导入令牌不落库。可空：老数据自动迁移。
    /// </summary>
    public string? FriendCode { get; set; }
}

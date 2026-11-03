#nullable enable
using System;
using Realms;

namespace Marisa.Database.Entity.Plugin.MaiMaiDx;

/// <summary>进行中的课题：每人每种目标最多一条，重新布置或重抽时覆盖。</summary>
public partial class MaiMaiDxKadai : IRealmObject, IHaveId, IHaveUId
{
    [PrimaryKey]
    public long Id { get; set; }

    /// <summary>被布置课题的玩家 QQ。</summary>
    [Indexed]
    public long UId { get; set; }

    /// <summary>目标名：SSS / SSS+ / AP / AP+ / 五星。</summary>
    public string Goal { get; set; } = string.Empty;

    /// <summary>筛选条件原文，重抽时沿用。</summary>
    public string Filter { get; set; } = string.Empty;

    /// <summary>三首谱面，格式 `歌曲ID:难度序号`，用逗号分隔，难度递增。</summary>
    public string Charts { get; set; } = string.Empty;

    public long AssignerId { get; set; }

    public string AssignerName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}

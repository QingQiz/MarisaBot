using System;
using Realms;

namespace Marisa.Database.Entity;

public partial class BlackList : IRealmObject, IHaveId, IHaveUId
{
    public BlackList()
    {
    }

    public BlackList(long uId)
    {
        UId     = uId;
        AddTime = DateTimeOffset.Now;
    }

    [PrimaryKey]
    public long Id { get; set; }

    [Indexed]
    public long UId { get; set; }

    public DateTimeOffset AddTime { get; set; }
}

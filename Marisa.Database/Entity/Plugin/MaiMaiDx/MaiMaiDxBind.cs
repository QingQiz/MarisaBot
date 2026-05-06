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
}

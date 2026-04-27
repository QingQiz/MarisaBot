using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Marisa.Database.Entity;
using Realms;

namespace Marisa.Database.Entity.Plugin.MaiMaiDx;

[Table("MaiMaiDx.Bind")]
public partial class MaiMaiDxBind : IRealmObject, IHaveUId
{
    public MaiMaiDxBind() {}

    public MaiMaiDxBind(long uid, int aimeId)
    {
        UId    = uid;
        AimeId = aimeId;
    }

    [Key]
    [PrimaryKey]
    public long Id { get; set; }

    [Indexed]
    public long UId { get; set; }

    public int AimeId { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Marisa.EntityFrameworkCore.Entity.Plugin.MaiMaiDx;

[Table("MaiMaiDx.Bind")]
public class MaiMaiDxBind : HaveUId
{
    public MaiMaiDxBind() {}

    public MaiMaiDxBind(long uid, int aimeId)
    {
        UId    = uid;
        AimeId = aimeId;
    }

    [Key]
    public long Id { get; set; }

    public int AimeId { get; set; }
}
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Marisa.EntityFrameworkCore.Entity.Plugin.MaiMaiDx;

[Table("MaiMaiDx.Bind")]
[Index(nameof(UId))]
public class MaiMaiDxBind
{
    [Key]
    public long Id { get; set; }

    public long UId { get; set; }
    
    public int AimeId { get; set; }

    public MaiMaiDxBind() {}

    public MaiMaiDxBind(long uid, int aimeId)
    {
        UId    = uid;
        AimeId = aimeId;
    }
}
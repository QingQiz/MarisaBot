using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Marisa.EntityFrameworkCore.Entity;

[Table("BlackList")]
public class BlackList : HaveUId
{
    public BlackList(long uId)
    {
        UId     = uId;
        AddTime = DateTime.Now;
    }

    [Key] public long Id { get; set; }

    public DateTime AddTime { get; set; }
}
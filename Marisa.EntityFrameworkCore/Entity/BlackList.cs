using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Marisa.EntityFrameworkCore.Entity;

[Table("BlackList")]
[Index(nameof(UId))]
public class BlackList
{
    [Key] public long Id { get; set; }
    
    public long UId { get; set; }

    public DateTime AddTime { get; set; }

    public BlackList(long uId)
    {
        UId = uId;
        AddTime = DateTime.Now;
    }
}
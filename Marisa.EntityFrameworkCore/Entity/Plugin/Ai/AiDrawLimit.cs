using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Marisa.EntityFrameworkCore.Entity.Plugin.Ai;

[Table("Ai.DrawLimit")]
[Index(nameof(UId), nameof(DateTime))]
public class AiDrawLimit
{
    [Key] 
    public long Id { get; set; }
    public long UId { get; set; }
    public DateTime DateTime { get; set; }

    public int UsedInPublic { get; set; }
    public int UsedInPrivate { get; set; }
    
    public AiDrawLimit(long uId)
    {
        UId = uId;
        DateTime = DateTime.Today;

        UsedInPrivate = 0;
        UsedInPublic  = 0;
    }
}
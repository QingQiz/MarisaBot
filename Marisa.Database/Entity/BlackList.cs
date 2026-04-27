using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Realms;

namespace Marisa.Database.Entity;

[Table("BlackList")]
public partial class BlackList : IRealmObject, IHaveUId
{
    public BlackList()
    {
    }

    public BlackList(long uId)
    {
        UId     = uId;
        AddTime = DateTimeOffset.Now;
    }

    [Key]
    [PrimaryKey]
    public long Id { get; set; }

    [Indexed]
    public long UId { get; set; }

    public DateTimeOffset AddTime { get; set; }
}

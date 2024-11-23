using Microsoft.EntityFrameworkCore;

namespace Marisa.EntityFrameworkCore.Entity;

[Index(nameof(UId))]
public class HaveUId
{
    /// <summary>
    ///     qq 号
    /// </summary>
    public long UId { get; set; }
}
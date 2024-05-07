using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Marisa.EntityFrameworkCore.Entity.Plugin.Chunithm;

[Table("Chunithm.Bind")]
[Index(nameof(UId))]
public class ChunithmBind
{
    [Key]
    public long Id { get; set; }

    public long UId { get; set; }

    [MaxLength(40)]
    public string AccessCode { get; set; }

    [MaxLength(255)]
    public string ServerName { get; set; }

    public ChunithmBind() {}

    public ChunithmBind(long uid, string serverName, string accessCode = "")
    {
        UId        = uid;
        ServerName = serverName;
        AccessCode = accessCode;
    }
}
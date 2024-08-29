using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Marisa.EntityFrameworkCore.Entity.Plugin.Chunithm;

[Table("Chunithm.Bind")]
public class ChunithmBind : HaveUId
{
    public ChunithmBind() {}

    public ChunithmBind(long uid, string serverName, string accessCode = "")
    {
        UId        = uid;
        ServerName = serverName;
        AccessCode = accessCode;
    }

    [Key]
    public long Id { get; set; }

    [MaxLength(40)]
    public string AccessCode { get; set; }

    [MaxLength(255)]
    public string ServerName { get; set; }
}
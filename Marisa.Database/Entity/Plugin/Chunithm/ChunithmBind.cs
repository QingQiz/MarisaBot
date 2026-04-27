using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Marisa.Database.Entity;
using Realms;

namespace Marisa.Database.Entity.Plugin.Chunithm;

[Table("Chunithm.Bind")]
public partial class ChunithmBind : IRealmObject, IHaveUId
{
    public ChunithmBind() {}

    public ChunithmBind(long uid, string serverName, string accessCode = "")
    {
        UId        = uid;
        ServerName = serverName;
        AccessCode = accessCode;
    }

    [Key]
    [PrimaryKey]
    public long Id { get; set; }

    [Indexed]
    public long UId { get; set; }

    [MaxLength(40)]
    public string AccessCode { get; set; } = string.Empty;

    [MaxLength(255)]
    public string ServerName { get; set; } = string.Empty;
}

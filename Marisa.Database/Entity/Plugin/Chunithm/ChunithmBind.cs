using System.ComponentModel.DataAnnotations;
using Marisa.Database.Entity;
using Realms;

namespace Marisa.Database.Entity.Plugin.Chunithm;

public partial class ChunithmBind : IRealmObject, IHaveId, IHaveUId
{
    public ChunithmBind() {}

    public ChunithmBind(long uid, string serverName, string accessCode = "")
    {
        UId        = uid;
        ServerName = serverName;
        AccessCode = accessCode;
    }

    [PrimaryKey]
    public long Id { get; set; }

    [Indexed]
    public long UId { get; set; }

    [MaxLength(40)]
    public string AccessCode { get; set; } = string.Empty;

    [MaxLength(255)]
    public string ServerName { get; set; } = string.Empty;
}

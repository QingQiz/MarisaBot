using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Realms;

namespace Marisa.Database.Entity;

[Table("CommandFilter")]
public partial class CommandFilter : IRealmObject
{
    [Key]
    [PrimaryKey]
    public long Id { get; set; }

    [Indexed]
    public long GroupId { get; set; }

    public string Prefix { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;
}

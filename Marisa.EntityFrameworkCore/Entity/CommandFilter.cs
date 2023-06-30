using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Marisa.EntityFrameworkCore.Entity;

[Table("CommandFilter")]
[Index(nameof(GroupId))]
public class CommandFilter
{
    [Key] public long Id { get; set; }
    public long GroupId { get; set; }
    public string Prefix { get; set; }
    public string Type { get; set; }
}
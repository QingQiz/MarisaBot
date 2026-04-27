using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Realms;

namespace Marisa.Database.Entity.Plugin;

[Table("Meal")]
public partial class Meal : IRealmObject
{
    public Meal(string place, string name)
    {
        Place = place;
        Name  = name;
    }

    public Meal()
    {
    }

    [Key]
    [PrimaryKey]
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    [Indexed]
    public string Place { get; set; } = string.Empty;
}

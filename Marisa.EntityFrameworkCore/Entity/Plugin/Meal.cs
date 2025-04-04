using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Marisa.EntityFrameworkCore.Entity.Plugin;

[Table("Meal")]
public class Meal
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
    public long Id { get; set; }
    public string Name { get; set; }
    public string Place { get; set; }
}
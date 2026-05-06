using Marisa.Database.Entity;
using Realms;

namespace Marisa.Database.Entity.Plugin;

public partial class Meal : IRealmObject, IHaveId
{
    public Meal(string place, string name)
    {
        Place = place;
        Name  = name;
    }

    public Meal()
    {
    }

    [PrimaryKey]
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    [Indexed]
    public string Place { get; set; } = string.Empty;
}

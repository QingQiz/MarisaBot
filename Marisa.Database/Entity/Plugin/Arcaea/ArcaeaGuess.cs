using Marisa.Database.Entity;
using Marisa.Database.Entity.Plugin.Shared;
using Realms;

namespace Marisa.Database.Entity.Plugin.Arcaea;

public partial class ArcaeaGuess : IRealmObject, IHaveId, ISongGuess
{
    public ArcaeaGuess()
    {
    }

    public ArcaeaGuess(long uid, string name)
    {
        UId = uid;
        Name = name;
    }

    [PrimaryKey]
    public long Id { get; set; }

    [Indexed]
    public long UId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int TimesStart { get; set; }

    public int TimesCorrect { get; set; }

    public int TimesWrong { get; set; }
}

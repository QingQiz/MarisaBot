using Marisa.Database.Entity;
using Marisa.Database.Entity.Plugin.Shared;
using Realms;

namespace Marisa.Database.Entity.Plugin.Ongeki;

public partial class OngekiGuess : IRealmObject, IHaveId, ISongGuess
{
    [PrimaryKey]
    public long Id { get; set; }

    [Indexed]
    public long UId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int TimesStart { get; set; }

    public int TimesCorrect { get; set; }

    public int TimesWrong { get; set; }

    public OngekiGuess() {}

    public OngekiGuess(long uid, string name)
    {
        UId = uid;
        Name = name;
    }
}

using Marisa.Database.Entity;
using Marisa.Database.Entity.Plugin.Shared;
using Realms;

namespace Marisa.Database.Entity.Plugin.Chunithm;

public partial class ChunithmGuess : IRealmObject, IHaveId, ISongGuess
{
    public ChunithmGuess()
    {
    }

    public ChunithmGuess(long uid, string name)
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

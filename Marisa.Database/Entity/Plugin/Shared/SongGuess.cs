using Marisa.Database.Entity;

namespace Marisa.Database.Entity.Plugin.Shared;

public interface ISongGuess : IHaveUId
{
    string Name { get; set; }
    int TimesStart { get; set; }
    int TimesCorrect { get; set; }
    int TimesWrong { get; set; }
}

public class SongGuess : ISongGuess
{
    public SongGuess()
    {
    }

    public SongGuess(long uid, string name)
    {
        UId        = uid;
        Name       = name;
        TimesStart = TimesCorrect = TimesWrong = 0;
    }

    public long UId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TimesStart { get; set; }
    public int TimesCorrect { get; set; }
    public int TimesWrong { get; set; }

    public T CastTo<T>() where T : ISongGuess, new()
    {
        return new T
        {
            UId          = UId,
            Name         = Name,
            TimesStart   = TimesStart,
            TimesCorrect = TimesCorrect,
            TimesWrong   = TimesWrong
        };
    }
}

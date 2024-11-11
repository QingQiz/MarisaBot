namespace Marisa.EntityFrameworkCore.Entity.Plugin.Shared;

public class SongGuess : HaveUId
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

    public string Name { get; set; }
    public int TimesStart { get; set; }
    public int TimesCorrect { get; set; }
    public int TimesWrong { get; set; }

    public T CastTo<T>() where T : SongGuess, new()
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
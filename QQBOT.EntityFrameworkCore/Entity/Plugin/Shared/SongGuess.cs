namespace QQBot.EntityFrameworkCore.Entity.Plugin.Shared;

public class SongGuess
{
    public long UId { get; set; }
    public string Name { get; set; }
    public int TimesStart { get; set; }
    public int TimesCorrect { get; set; }
    public int TimesWrong { get; set; }
}
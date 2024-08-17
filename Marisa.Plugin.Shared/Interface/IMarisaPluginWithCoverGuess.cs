using Marisa.EntityFrameworkCore;
using Marisa.EntityFrameworkCore.Entity.Plugin.Shared;
using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.Shared.Interface;

public interface IMarisaPluginWithCoverGuess<TSong, TSongGuess> where TSong : Song where TSongGuess : SongGuess, new()
{
    SongDb<TSong, TSongGuess> SongDb { get; }

    /// <summary>
    ///     猜歌排名
    /// </summary>
    [MarisaPluginDoc("猜歌排名，给出的结果中s,c,w分别是启动猜歌的次数，猜对的次数和猜错的次数")]
    [MarisaPluginSubCommand(nameof(Guess))]
    [MarisaPluginCommand(true, "排名")]
    MarisaPluginTaskState GuessRank(Message message)
    {
        using var dbContext = new BotDbContext();

        var res = dbContext.OngekiGuesses
            .OrderByDescending(g => g.TimesCorrect)
            .ThenBy(g => g.TimesWrong)
            .ThenBy(g => g.TimesStart)
            .Take(10)
            .ToList();

        if (res.Count == 0) message.Reply("None");

        message.Reply(string.Join('\n', res.Select((guess, i) =>
            $"{i + 1}、 {guess.Name}： (s:{guess.TimesStart}, w:{guess.TimesWrong}, c:{guess.TimesCorrect})")));

        return MarisaPluginTaskState.CompletedTask;
    }

    /// <summary>
    ///     猜歌
    /// </summary>
    [MarisaPluginDoc("看封面猜曲")]
    [MarisaPluginCommand(MessageType.GroupMessage, StringComparison.OrdinalIgnoreCase, "猜歌", "猜曲", "guess")]
    MarisaPluginTaskState Guess(Message message, long qq)
    {
        if (message.Command.IsEmpty)
        {
            SongDb.StartSongCoverGuess(message, qq, 3, null);
        }
        else
        {
            message.Reply("错误的命令格式");
        }

        return MarisaPluginTaskState.CompletedTask;
    }
}
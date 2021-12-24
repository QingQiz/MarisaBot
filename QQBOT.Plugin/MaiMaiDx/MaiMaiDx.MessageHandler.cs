using System.Text.RegularExpressions;
using Flurl.Http;
using QQBot.EntityFrameworkCore;
using QQBot.MiraiHttp;
using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Plugin;
using QQBot.MiraiHttp.Util;
using QQBot.Plugin.Shared.MaiMaiDx;

namespace QQBot.Plugin.MaiMaiDx;

[MiraiPlugin(20)]
[MiraiPluginCommand(StringComparison.OrdinalIgnoreCase, "maimai", "mai", "舞萌")]
public partial class MaiMaiDx : MiraiPluginBase
{
    /// <summary>
    /// b40
    /// </summary>
    [MiraiPluginCommand(StringComparison.OrdinalIgnoreCase, "b40", "查分")]
    private async Task<MiraiPluginTaskState> MaiMaiDxB40(Message message, MessageSenderProvider ms)
    {
        var username = message.Command;
        var sender   = message.Sender!.Id;

        MessageChain ret;
        try
        {
            var response = await "https://www.diving-fish.com/api/maimaidxprober/query/player".PostJsonAsync(
                string.IsNullOrEmpty(username)
                    ? new { qq = sender }
                    : new { username });

            ret = MessageChain.FromImageB64(new DxRating(await response.GetJsonAsync()).GetImage());
        }
        catch (FlurlHttpException e) when (e.StatusCode == 400)
        {
            ret = MessageChain.FromPlainText("“查无此人”");
        }
        catch (FlurlHttpException e) when (e.StatusCode == 403)
        {
            ret = MessageChain.FromPlainText("“403 forbidden”");
        }

        ms.SendByRecv(ret, message);

        return MiraiPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// 搜歌
    /// </summary>
    [MiraiPluginCommand(StringComparison.OrdinalIgnoreCase, "song", "search", "搜索")]
    private MiraiPluginTaskState MaiMaiDxSearchSong(Message message, MessageSenderProvider ms)
    {
        var search = SearchSong(message.Command);

        ms.SendByRecv(GetSearchResult(search), message);

        return MiraiPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// 搜歌，但是限定使用ID
    /// </summary>
    [MiraiPluginCommand(StringComparison.OrdinalIgnoreCase, "id")]
    private MiraiPluginTaskState MaiMaiDxGetSongById(Message message, MessageSenderProvider ms)
    {
        ms.SendByRecv(
            long.TryParse(message.Command, out var id)
                ? GetSongInfo(id)
                : MessageChain.FromPlainText("“你看你输的这个几把玩意儿像不像个ID”"), message);

        return MiraiPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// 随机给出一个歌
    /// </summary>
    [MiraiPluginCommand(StringComparison.OrdinalIgnoreCase, "random", "随机", "rand")]
    private MiraiPluginTaskState MaiMaiDxRandomSong(Message message, MessageSenderProvider ms)
    {
        var list = ListSongs(message.Command);

        ms.SendByRecv(list.Count == 0
            ? MessageChain.FromPlainText("“NULL”")
            : MessageChain.FromImageB64(list[new Random().Next(list.Count)].GetImage()), message);

        return MiraiPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// 给出歌曲列表
    /// </summary>
    [MiraiPluginCommand(StringComparison.OrdinalIgnoreCase, "list")]
    private MiraiPluginTaskState MaiMaiDxListSong(Message message, MessageSenderProvider ms)
    {
        var          list = ListSongs(message.Command);
        MessageChain ret;

        if (list.Count == 0)
        {
            ret = MessageChain.FromPlainText("“EMPTY”");
        }
        else
        {
            var rand = new Random();
            var str = string.Join('\n',
                list.OrderBy(_ => rand.Next())
                    .Take(15)
                    .OrderBy(x => x.Id)
                    .Select(song => $"[T:{song.Type}, ID:{song.Id}] -> {song.Title}"));

            if (list.Count > 15) str += "\n" + $"太多了（{list.Count}），随机给出15个";

            ret = MessageChain.FromPlainText(str);
        }

        ms.SendByRecv(ret, message);

        return MiraiPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// 舞萌猜歌
    /// </summary>
    [MiraiPluginCommand(MiraiMessageType.GroupMessage, StringComparison.OrdinalIgnoreCase, "猜歌", "猜曲", "guess")]
    private MiraiPluginTaskState MaiMaiDxGuess(Message message, MessageSenderProvider ms, long qq)
    {
        switch (message.Command)
        {
            case "排名":
            {
                using var dbContext = new BotDbContext();

                var res = dbContext.MaiMaiDxGuesses
                    .OrderByDescending(g => g.TimesCorrect)
                    .ThenBy(g => g.TimesWrong)
                    .ThenBy(g => g.TimesStart)
                    .Take(10)
                    .ToList();

                if (!res.Any()) ms.SendByRecv(MessageChain.FromPlainText("None"), message);

                ms.SendByRecv(MessageChain.FromPlainText(
                        string.Join('\n', res.Select((guess, i) =>
                            $"{i + 1}、 {guess.Name}： (s:{guess.TimesStart}, w:{guess.TimesWrong}, c:{guess.TimesCorrect})"))),
                    message);
                break;
            }
            case "v2":
            {
                StartSongSoundGuess(message, ms, qq);
                break;
            }
            default:
            {
                if (message.Command.StartsWith("c:", StringComparison.OrdinalIgnoreCase))
                {
                    var reg = message.Command[2..];
                    if (!reg.IsRegex())
                    {
                        break;
                    }

                    StartSongCoverGuess(message, ms, qq,
                        new Regex(reg, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace));
                }
                else if (message.Command == "")
                {
                    StartSongCoverGuess(message, ms, qq, null);
                }

                break;
            }
        }

        return MiraiPluginTaskState.CompletedTask;
    }

    /// <summary>
    /// 别名处理
    /// </summary>
    [MiraiPluginCommand(StringComparison.OrdinalIgnoreCase, "alias")]
    private MiraiPluginTaskState MaiMaiDxSongAlias(Message message, MessageSenderProvider ms)
    {
        var mc = SongAliasHandler(message.Command);
        
        if (mc != null) ms.SendByRecv(mc, message);

        return MiraiPluginTaskState.CompletedTask;
    }
}
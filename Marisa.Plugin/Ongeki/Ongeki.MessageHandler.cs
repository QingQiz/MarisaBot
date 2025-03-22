using System.Diagnostics.CodeAnalysis;
using Marisa.Plugin.Shared.Dialog;
using Marisa.Plugin.Shared.Ongeki;
using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.Ongeki;

[SuppressMessage("ReSharper", "UnusedMember.Local")]
public partial class Ongeki
{
    #region 分数线 / 容错率

    [MarisaPluginDoc("计算某首歌曲的容错率，参数为：歌名")]
    [MarisaPluginCommand("tolerance", "容错率")]
    private async Task<MarisaPluginTaskState> FaultTolerance(Message message)
    {
        var songName     = message.Command.Trim();
        var searchResult = SongDb.SearchSong(songName);

        var song = await SongDb.MultiPageSelectResult(searchResult, message, false, true);
        if (song == null)
        {
            return MarisaPluginTaskState.CompletedTask;
        }

        message.Reply("难度和预期达成率？");
        await DialogManager.AddDialogAsync((message.GroupInfo?.Id, message.Sender.Id), next =>
        {
            var command = next.Command.Trim();

            var levelName = OngekiSong.LevelAlias.Values.ToList();

            // 全名
            var level       = levelName.FirstOrDefault(n => command.StartsWith(n, StringComparison.OrdinalIgnoreCase));
            var levelPrefix = level ?? "";
            if (level != null) goto RightLabel;

            // 首字母
            level = levelName.FirstOrDefault(n =>
                command.StartsWith(n[0].ToString(), StringComparison.OrdinalIgnoreCase));
            if (level != null)
            {
                levelPrefix = command.Span[0].ToString();
                goto RightLabel;
            }

            // 别名
            level = OngekiSong.LevelAlias.Keys.FirstOrDefault(a =>
                command.StartsWith(a, StringComparison.OrdinalIgnoreCase));
            levelPrefix = level ?? "";
            if (level != null)
            {
                level = OngekiSong.LevelAlias[level];
                goto RightLabel;
            }

            next.Reply("错误的难度格式，会话已关闭。可用难度格式：难度全名、难度全名的首字母或难度颜色");
            return Task.FromResult(MarisaPluginTaskState.CompletedTask);

            RightLabel:
            var levelIdx = levelName.IndexOf(level);

            if (levelIdx > song.Charts.Count - 1 ||
                song.Charts[levelIdx] is null ||
                song.Charts[levelIdx]!.NoteCount == 0 ||
                song.Charts[levelIdx]!.BellCount == 0)
            {
                next.Reply("暂无该难度的数据");
                return Task.FromResult(MarisaPluginTaskState.CompletedTask);
            }

            var parseSuccess = int.TryParse(command[levelPrefix.Length..].Span, out var achievement);

            if (!parseSuccess)
            {
                next.Reply("错误的达成率格式，会话已关闭");
                return Task.FromResult(MarisaPluginTaskState.CompletedTask);
            }

            if (achievement is > 101_0000 or < 0)
            {
                next.Reply("你查**呢");
                return Task.FromResult(MarisaPluginTaskState.CompletedTask);
            }

            var noteScore = 95_0000m / song.Charts[levelIdx]!.NoteCount;
            var bellScore = 6_0000m / song.Charts[levelIdx]!.BellCount;

            var @break = 0.1m * noteScore;
            var hit    = 0.4m * noteScore;

            var tolerance = 101_0000 - achievement;

            next.Reply(
                new MessageDataText($"[{levelName[levelIdx]}] {song.Title} => {achievement}\n"),
                new MessageDataText($"至多有 {tolerance / @break:F1} 个 break，每个 break 减 {@break:F1} 分\n"),
                new MessageDataText($"至多有 {tolerance / hit:F1} 个 hit，每个 hit 减 {hit:F1} 分\n"),
                new MessageDataText($"至多有 {tolerance / noteScore:F1} 个 miss，每个 miss 减 {noteScore:F1} 分\n\n"),
                new MessageDataText($"每个 bell 有 {bellScore:F1} 分\n"),
                new MessageDataText($"1 bell 相当于 {bellScore / @break:F1} break\n"),
                new MessageDataText($"1 bell 相当于 {bellScore / hit:F1} hit\n"),
                new MessageDataText($"1 bell 相当于 {bellScore / noteScore:F1} miss\n")
            );

            return Task.FromResult(MarisaPluginTaskState.CompletedTask);
        });

        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion
}
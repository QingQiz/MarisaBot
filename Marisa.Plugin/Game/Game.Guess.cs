﻿using System.Diagnostics;
using System.Dynamic;
using System.Text;
using System.Text.RegularExpressions;
using Flurl.Http;
using Marisa.Plugin.Shared.Chunithm;
using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.Util;
using Newtonsoft.Json;
using osu.Game.Extensions;
using ResourceManager = Marisa.Plugin.Shared.Chunithm.ResourceManager;

namespace Marisa.Plugin.Game;

public partial class Game
{

    private static readonly List<string> GuessDbName = new List<string>
    {
        "maimai",
        "chunithm"
    }.Concat(Directory.GetFiles(GuessDbPath).Select(Path.GetFileName).Cast<string>()).ToList();

    private static readonly Func<int, Func<string[]>> GuessDbReader = idx => idx switch
    {
        0 => () =>
        {
            var data = "https://www.diving-fish.com/api/maimaidxprober/music_data".GetJsonListAsync().Result;
            return data.Select(d => new MaiMaiSong(d)).Select(x => x.Title).ToArray();
        },
        1 => () =>
        {
            var data =
                JsonConvert.DeserializeObject<ExpandoObject[]>(
                    File.ReadAllText(ResourceManager.ResourcePath + "/SongInfo.json")) as dynamic[];
            return data!.Select(d => new ChunithmSong(d)).Select(x => x.Title).ToArray();
        },
        _ => () =>
        {
            var dbName = GuessDbName[idx];
            return File.ReadAllLines(Path.Join(GuessDbPath, dbName)).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        }
    };
    private static string GuessDbPath => Path.Join(ConfigurationManager.Configuration.Game.TempPath, "Guess");

    [MarisaPluginDoc("添加曲库，仅私聊可用，参数：曲库名字")]
    [MarisaPluginSubCommand(nameof(Guess))]
    [MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, "add")]
    private static MarisaPluginTaskState GuessAddDb(Message message, long qq)
    {
        if (message.GroupInfo != null) return MarisaPluginTaskState.CompletedTask;

        var dbName = message.Command.Trim().ToString();

        switch (dbName)
        {
            case "":
            case { Length: > 20 }:
            case not null when dbName.Any(c => Path.GetInvalidFileNameChars().Contains(c)):
                message.Reply("曲库名字不合法");
                return MarisaPluginTaskState.CompletedTask;
        }

        if (File.Exists(Path.Join(GuessDbPath, dbName)))
        {
            message.Reply("已经存在的曲库");
            return MarisaPluginTaskState.CompletedTask;
        }

        message.Reply("请给出要猜的单词，每行一个，可以分多次回复\n发送“结束”结束\n发送“取消”取消\n所有歌名都必须匹配如下正则表达式：\n" + SongTitleMatcher());

        var res = new HashSet<ReadOnlyMemory<char>>([], new MemoryExt.ReadOnlyMemoryCharComparer(StringComparison.OrdinalIgnoreCase));

        Dialog.AddHandler(message.GroupInfo?.Id, message.Sender.Id, mNext =>
        {
            switch (mNext.Command.Span)
            {
                case "结束" when res.Count < 20:
                    mNext.Reply("太少了，最少20个，请继续", false);
                    return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
                case "结束":
                    File.WriteAllLines(Path.Join(GuessDbPath, dbName), res.Select(x => x.ToString()), Encoding.UTF8);
                    mNext.Reply("完成", false);
                    return Task.FromResult(MarisaPluginTaskState.CompletedTask);
                case "取消":
                    mNext.Reply("行吧", false);
                    return Task.FromResult(MarisaPluginTaskState.CompletedTask);
            }

            var titles = mNext.Command
                .Split('\n')
                .Select(x => x.Trim())
                .Where(x => !x.IsEmpty)
                .Distinct(StringComparison.OrdinalIgnoreCase)
                .ToArray();

            var illegalTitles = titles.Where(t => !SongTitleMatcher().IsMatch(t.ToString())).ToArray();
            if (illegalTitles.Length != 0)
            {
                mNext.Reply($"不合法的标题，此次所有的都无效，请重试：{illegalTitles.First()}");
                return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
            }

            res.AddRange(titles);
            mNext.Reply("继续", false);

            return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
        });

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("一种新的猜歌游戏，参数为数据库名，可写多个，用:分隔，仅群聊可用")]
    [MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, "guess")]
    private static MarisaPluginTaskState Guess(Message message)
    {
        if (message.GroupInfo == null) return MarisaPluginTaskState.CompletedTask;

        if (!ReadTitles(message, out var songName, out var marisaPluginTaskState)) return marisaPluginTaskState;

        Debug.Assert(songName != null, nameof(songName) + " != null");

        var tips  = new HashSet<char>();
        var right = new HashSet<int>();

        var cooldown       = new Dictionary<long, DateTime>();
        var cooldownGlobal = DateTime.MinValue;

        var res = Dialog.AddHandler(message.GroupInfo?.Id, null, mNext =>
        {
            if (mNext.Command.Span is "结束游戏")
            {
                return Task.FromResult(MarisaPluginTaskState.CompletedTask);
            }

            if (mNext.Command.StartsWith("开") && mNext.Command.Length == 2)
            {
                if (!SongTitleMatcher().IsMatch(mNext.Command[1..].ToString()))
                {
                    return Task.FromResult(MarisaPluginTaskState.NoResponse);
                }

                if (DateTime.Now - cooldownGlobal < TimeSpan.FromMinutes(1))
                {
                    return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
                }

                if (cooldown.TryGetValue(mNext.Sender.Id, out var t))
                {
                    if (DateTime.Now - t < TimeSpan.FromMinutes(3))
                    {
                        return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
                    }
                }

                if (tips.Contains(mNext.Command.Span[1]))
                {
                    return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
                }

                cooldown[mNext.Sender.Id] = DateTime.Now;
                cooldownGlobal            = DateTime.Now;

                tips.Add(char.ToLower(mNext.Command.Span[1]));
                tips.Add(char.ToUpper(mNext.Command.Span[1]));
                mNext.Reply(Reply(), false);
            }
            else
            {
                var idx = mNext.Command.Span.IndexOfAny(new[] { ':', '：' });
                if (idx == -1) return Task.FromResult(MarisaPluginTaskState.NoResponse);

                var numStr = mNext.Command[..idx].Trim();
                var name   = mNext.Command[(idx + 1)..].Trim();

                if (!int.TryParse(numStr.Span, out var num)) return Task.FromResult(MarisaPluginTaskState.NoResponse);
                if (num <= 0 || num > songName.Count) return Task.FromResult(MarisaPluginTaskState.NoResponse);

                if (name.Length != songName[num - 1].Length)
                {
                }
                else
                {
                    if (!name.Equals(songName[num - 1], StringComparison.OrdinalIgnoreCase)) return Task.FromResult(MarisaPluginTaskState.ToBeContinued);

                    if (!right.Add(num - 1)) return Task.FromResult(MarisaPluginTaskState.ToBeContinued);

                    if (right.Count == songName.Count)
                    {
                        mNext.Reply("全部猜出来了耶！", false);
                        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
                    }

                    mNext.Reply("对对对");
                }
            }

            return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
        });

        if (res)
        {
            message.Reply($"猜歌游戏开始！\n{Reply()}发送“开`任意字符`”开\n发送“`序号`:`歌名`”猜", false);
        }
        else
        {
            message.Reply("？");
        }

        return MarisaPluginTaskState.CompletedTask;

        string Reply() => ReplyGenerator(songName, right, tips);
    }

    private static string ReplyGenerator(IReadOnlyList<string> songName, IReadOnlySet<int> right, IReadOnlySet<char> tips)
    {
        var sb = new StringBuilder();

        for (var i = 0; i < songName.Count; i++)
        {
            if (right.Contains(i)) continue;

            sb.Append($"{"①②③④⑤⑥⑦⑧⑨⑩⑪⑫⑬⑭⑮⑯⑰⑱⑲⑳"[i]}: ");

            foreach (var c in songName[i])
            {
                if (tips.Contains(c) || c == ' ')
                    sb.Append(c);
                else
                    sb.Append('#');
            }

            sb.AppendLine();
        }

        if (tips.Any())
        {
            sb.Append("开了的：" + string.Join("", tips));
        }

        return sb.ToString();
    }

    private static bool ReadTitles(Message message, out List<string>? songName, out MarisaPluginTaskState marisaPluginTaskState)
    {
        songName              = null;
        marisaPluginTaskState = MarisaPluginTaskState.NoResponse;

        var db = message.Command.Split(':').ToArray();

        if (db.Any(x => !GuessDbName.Contains(x, StringComparison.OrdinalIgnoreCase)))
        {
            message.Reply($"可用的数据库：{string.Join(',', GuessDbName)}");
            {
                marisaPluginTaskState = MarisaPluginTaskState.CompletedTask;
                return false;
            }
        }

        songName = new List<string>();

        for (var i = 0; i < GuessDbName.Count; i++)
        {
            if (db.Contains(GuessDbName[i], StringComparison.OrdinalIgnoreCase))
            {
                songName.AddRange(GuessDbReader(i)());
            }
        }

        var regex = SongTitleMatcher();

        songName = songName.Distinct(StringComparer.OrdinalIgnoreCase).Where(x => regex.IsMatch(x)).RandomTake(15).ToList();
        return true;
    }

    [GeneratedRegex("^[|a-zA-Z0-9,./?():;'\"*!@#$%^&-_=+`~<> ]+$")]
    private static partial Regex SongTitleMatcher();
}
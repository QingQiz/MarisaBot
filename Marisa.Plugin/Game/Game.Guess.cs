using System.Diagnostics;
using System.Dynamic;
using System.Text;
using System.Text.RegularExpressions;
using Flurl.Http;
using Marisa.Plugin.Shared.Chunithm;
using Marisa.Plugin.Shared.MaiMaiDx;
using Newtonsoft.Json;

namespace Marisa.Plugin.Game;

public partial class Game
{
    private static string GuessDbPath => Path.Join(ConfigurationManager.Configuration.Game.TempPath, "Guess");

    private static readonly List<string> GuessDbName = new List<string>
    {
        "maimai",
        "chunithm",
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
                    File.ReadAllText(Shared.Chunithm.ResourceManager.ResourcePath + "/SongInfo.json")) as dynamic[];
            return data!.Select(d => new ChunithmSong(d)).Select(x => x.Title).ToArray();
        },
        _ => () =>
        {
            var dbName = GuessDbName[idx];
            return File.ReadAllLines(Path.Join(GuessDbPath, dbName)).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        }
    };

    [MarisaPluginDoc("添加曲库，仅私聊可用，参数：曲库名字")]
    [MarisaPluginSubCommand(nameof(Guess))]
    [MarisaPluginCommand(MessageType.FriendMessage, StringComparison.OrdinalIgnoreCase, "add")]
    private static MarisaPluginTaskState GuessAddDb(Message message, long qq)
    {
        var dbName = message.Command.Trim();

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

        message.Reply("请给出要猜的单词，每行一个，所有歌名都必须匹配如下正则表达式：\n" + SongTitleMatcher());

        Dialog.AddHandler(message.GroupInfo?.Id, message.Sender?.Id, mNext =>
        {
            var titles = mNext.Command
                .Split('\n')
                .Select(x => x.Trim())
                .Where(x => x != "")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var illegalTitles = titles.Where(t => !SongTitleMatcher().IsMatch(t)).ToArray();
            if (illegalTitles.Any())
            {
                mNext.Reply("不合法的标题：\n" + string.Join('\n', illegalTitles));
                return Task.FromResult(MarisaPluginTaskState.CompletedTask);
            }

            File.WriteAllLines(Path.Join(GuessDbPath, dbName), titles, Encoding.UTF8);
            mNext.Reply("完成");

            return Task.FromResult(MarisaPluginTaskState.CompletedTask);
        });

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("严格模式，需要报名和排队")]
    [MarisaPluginSubCommand(nameof(Guess))]
    [MarisaPluginCommand(MessageType.GroupMessage, StringComparison.OrdinalIgnoreCase, "strict")]
    private static MarisaPluginTaskState GuessStrict(Message message)
    {
        if (!ReadTitles(message, out var songName, out var marisaPluginTaskState)) return marisaPluginTaskState;

        Debug.Assert(songName != null, nameof(songName) + " != null");

        var tips  = new HashSet<char>();
        var right = new HashSet<int>();
        var queue = new Queue<long>();

        queue.Enqueue(message.Sender!.Id);

        string Reply() => ReplyGenerator(songName, right, tips);

        var res = Dialog.AddHandler(message.GroupInfo?.Id, null, mNext =>
        {
            switch (mNext.Command)
            {
                case "结束游戏":
                    return Task.FromResult(MarisaPluginTaskState.CompletedTask);
                case "我要参加" when !queue.Contains(mNext.Sender!.Id):
                    mNext.Reply("ok");
                    queue.Enqueue(mNext.Sender!.Id);
                    return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
            }

            if (mNext.Sender!.Id != queue.Peek())
            {
                return Task.FromResult(MarisaPluginTaskState.NoResponse);
            }

            var reply = "";

            if (mNext.Command.StartsWith("开") && mNext.Command.Length == 2)
            {
                if (!SongTitleMatcher().IsMatch(mNext.Command[1..]))
                {
                    return Task.FromResult(MarisaPluginTaskState.NoResponse);
                }

                if (tips.Contains(mNext.Command[1]))
                {
                    return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
                }

                queue.Dequeue();
                queue.Enqueue(mNext.Sender!.Id);

                tips.Add(char.ToLower(mNext.Command[1]));
                tips.Add(char.ToUpper(mNext.Command[1]));

                reply += Reply();
            }
            else
            {
                var idx = mNext.Command.IndexOfAny(new[] { ':', '：' });
                if (idx == -1) return Task.FromResult(MarisaPluginTaskState.NoResponse);

                var numStr = mNext.Command[..idx].Trim();
                var name   = mNext.Command[(idx + 1)..].Trim();

                if (!int.TryParse(numStr, out var num)) return Task.FromResult(MarisaPluginTaskState.NoResponse);
                if (num <= 0 || num > songName.Count) return Task.FromResult(MarisaPluginTaskState.NoResponse);

                queue.Dequeue();
                queue.Enqueue(mNext.Sender!.Id);

                if (name.Equals(songName[num - 1], StringComparison.OrdinalIgnoreCase))
                {
                    if (!right.Contains(num - 1))
                    {
                        right.Add(num - 1);

                        if (right.Count == songName.Count)
                        {
                            mNext.Reply("全部猜出来了耶！", false);
                            return Task.FromResult(MarisaPluginTaskState.CompletedTask);
                        }

                        reply += "对对对";
                    }
                }
            }

            mNext.Send(new MessageDataText($"{reply}{(string.IsNullOrWhiteSpace(reply) ? "" : "\n")}轮到"), new MessageDataAt(queue.Peek()), new MessageDataText("了"));

            return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
        });

        if (res)
        {
            message.Send(
                new MessageDataText($"猜歌游戏开始！\n{Reply()}发送“开`任意字符`”开\n发送“`序号`:`歌名`”猜\n发送“我要参加”参加猜歌排队\n"),
                new MessageDataText("轮到"), new MessageDataAt(message.Sender!.Id), new MessageDataText("了")
            );
        }
        else
        {
            message.Reply("？");
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("一种新的猜歌游戏，参数为数据库名，可写多个，用:分隔，仅群聊可用（非严格模式）")]
    [MarisaPluginCommand(MessageType.GroupMessage, StringComparison.OrdinalIgnoreCase, "guess")]
    private static MarisaPluginTaskState Guess(Message message)
    {
        if (!ReadTitles(message, out var songName, out var marisaPluginTaskState)) return marisaPluginTaskState;

        Debug.Assert(songName != null, nameof(songName) + " != null");

        var tips  = new HashSet<char>();
        var right = new HashSet<int>();

        var cooldown       = new Dictionary<long, DateTime>();
        var cooldownGlobal = DateTime.MinValue;

        string Reply() => ReplyGenerator(songName, right, tips);

        var res = Dialog.AddHandler(message.GroupInfo?.Id, null, mNext =>
        {
            if (mNext.Command == "结束游戏")
            {
                return Task.FromResult(MarisaPluginTaskState.CompletedTask);
            }

            if (mNext.Command.StartsWith("开") && mNext.Command.Length == 2)
            {
                if (!SongTitleMatcher().IsMatch(mNext.Command[1..]))
                {
                    return Task.FromResult(MarisaPluginTaskState.NoResponse);
                }

                if (DateTime.Now - cooldownGlobal < TimeSpan.FromMinutes(1))
                {
                    return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
                }

                if (cooldown.TryGetValue(mNext.Sender!.Id, out var t))
                {
                    if (DateTime.Now - t < TimeSpan.FromMinutes(3))
                    {
                        return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
                    }
                }

                if (tips.Contains(mNext.Command[1]))
                {
                    return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
                }

                cooldown[mNext.Sender!.Id] = DateTime.Now;
                cooldownGlobal             = DateTime.Now;

                tips.Add(char.ToLower(mNext.Command[1]));
                tips.Add(char.ToUpper(mNext.Command[1]));
                mNext.Reply(Reply(), false);
            }
            else
            {
                var idx = mNext.Command.IndexOfAny(new[] { ':', '：' });
                if (idx == -1) return Task.FromResult(MarisaPluginTaskState.NoResponse);

                var numStr = mNext.Command[..idx].Trim();
                var name   = mNext.Command[(idx + 1)..].Trim();

                if (!int.TryParse(numStr, out var num)) return Task.FromResult(MarisaPluginTaskState.NoResponse);
                if (num <= 0 || num > songName.Count) return Task.FromResult(MarisaPluginTaskState.NoResponse);

                if (name.Length != songName[num - 1].Length)
                {
                }
                else
                {
                    if (name.Equals(songName[num - 1], StringComparison.OrdinalIgnoreCase))
                    {
                        if (!right.Contains(num - 1))
                        {
                            right.Add(num - 1);

                            if (right.Count == songName.Count)
                            {
                                mNext.Reply($"全部猜出来了耶！", false);
                                return Task.FromResult(MarisaPluginTaskState.CompletedTask);
                            }

                            mNext.Reply("对对对");
                        }
                    }
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

        var db = message.Command.Split(':');

        if (db.Any(x => !GuessDbName.Contains(x, StringComparer.OrdinalIgnoreCase)))
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
            if (db.Contains(GuessDbName[i], StringComparer.OrdinalIgnoreCase))
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
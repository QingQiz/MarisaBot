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
    private static readonly string[] GuessDbName =
    {
        "maimai",
        "chunithm"
    };

    private static readonly Func<string[]>[] GuessDbReader =
    {
        () =>
        {
            var data = "https://www.diving-fish.com/api/maimaidxprober/music_data".GetJsonListAsync().Result;
            return data.Select(d => new MaiMaiSong(d)).Select(x => x.Title).ToArray();
        },
        () =>
        {
            var data = JsonConvert.DeserializeObject<ExpandoObject[]>(
                File.ReadAllText(Shared.Chunithm.ResourceManager.ResourcePath + "/SongInfo.json")
            ) as dynamic[];
            return data!.Select(d => new ChunithmSong(d)).Select(x => x.Title).ToArray();
        }
    };

    [MarisaPluginDoc("一种新的猜歌游戏，参数为数据库名，可写多个，用空格分隔")]
    [MarisaPluginCommand(MessageType.GroupMessage, StringComparison.OrdinalIgnoreCase, "guess", "猜歌")]
    private static MarisaPluginTaskState Guess(Message message, long qq)
    {
        var db = message.Command.Split(' ');

        if (db.Any(x => !GuessDbName.Contains(x, StringComparer.OrdinalIgnoreCase)))
        {
            message.Reply($"可用的数据库：{string.Join(',', GuessDbName)}");
            return MarisaPluginTaskState.CompletedTask;
        }

        var songName = new List<string>();

        for (var i = 0; i < GuessDbName.Length; i++)
        {
            if (db.Contains(GuessDbName[i], StringComparer.OrdinalIgnoreCase))
            {
                songName.AddRange(GuessDbReader[i]());
            }
        }

        var regex = SongTitleMatcher();

        songName = songName.Distinct(StringComparer.OrdinalIgnoreCase).Where(x => regex.IsMatch(x)).RandomTake(15).ToList();

        var tips  = new HashSet<char>();
        var right = new HashSet<int>();

        var startTime = DateTime.Now;

        string ReplyGenerator()
        {
            const string header = "猜歌游戏开始！\n";
            const string footer = "回复“开`任意字符`”并@bot获取提示\n回复“`序号`:`歌名`”并@bot进行猜曲";

            var sb = new StringBuilder();

            sb.Append(header);
            for (var i = 0; i < songName.Count; i++)
            {
                sb.Append($"{i + 1}、");

                if (right.Contains(i))
                {
                    sb.AppendLine(songName[i]);
                }
                else
                {
                    foreach (var c in songName[i])
                    {
                        if (tips.Contains(c) || c == ' ')
                            sb.Append(c);
                        else
                            sb.Append('#');
                    }

                    sb.AppendLine();
                }
            }

            sb.Append(footer);

            return sb.ToString();
        }

        var res = Dialog.AddHandler(message.GroupInfo?.Id, null, mNext =>
        {
            switch (mNext.Command)
            {
                case "结束猜曲" or "答案":
                {
                    mNext.Reply("猜曲结束");
                    return Task.FromResult(MarisaPluginTaskState.CompletedTask);
                }
            }

            if (!mNext.IsAt(qq))
            {
                // continue
                if (DateTime.Now - startTime <= TimeSpan.FromMinutes(15)) return Task.FromResult(MarisaPluginTaskState.NoResponse);

                // time out
                mNext.Reply("猜曲已结束", false);
                return Task.FromResult(MarisaPluginTaskState.Canceled);
            }

            if (mNext.Command.StartsWith("开"))
            {
                tips.Add(char.ToLower(mNext.Command[1]));
                tips.Add(char.ToUpper(mNext.Command[1]));
                mNext.Reply(ReplyGenerator(), false);
            }
            else
            {
                var idx = mNext.Command.IndexOfAny(new[] { ':', '：' });
                if (idx == -1) return Task.FromResult(MarisaPluginTaskState.NoResponse);

                var numStr = mNext.Command[..idx].Trim();
                var name   = mNext.Command[(idx + 1)..].Trim();
                if (int.TryParse(numStr, out var num))
                {
                    if (num <= 0 || num > songName.Count)
                    {
                        mNext.Reply("序号错误");
                    }
                    else
                    {
                        if (name.Length != songName[num - 1].Length)
                        {
                            mNext.Reply("歌名长度错误");
                        }
                        else
                        {
                            if (name.Equals(songName[num - 1], StringComparison.OrdinalIgnoreCase))
                            {
                                mNext.Reply("答对了！");
                                right.Add(num - 1);
                                mNext.Reply(ReplyGenerator(), false);

                                if (right.Count == songName.Count)
                                {
                                    mNext.Reply("全部猜出来了耶");
                                    return Task.FromResult(MarisaPluginTaskState.CompletedTask);
                                }
                            }
                            else
                            {
                                mNext.Reply("错错错");
                            }
                        }
                    }
                }
                else
                {
                    mNext.Reply("序号错误");
                }
            }

            return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
        });

        if (res)
        {
            message.Reply(ReplyGenerator(), false);
        }
        else
        {
            message.Reply("？");
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    [GeneratedRegex("^[a-zA-Z0-9,./?():;'\"*!@#$%^&-_=+`~ ]+$")]
    private static partial Regex SongTitleMatcher();
}
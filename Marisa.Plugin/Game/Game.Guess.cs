using System.Dynamic;
using System.Text;
using System.Text.RegularExpressions;
using Flurl.Http;
using Marisa.Plugin.Shared.Chunithm;
using Marisa.Plugin.Shared.MaiMaiDx;
using Newtonsoft.Json;
using SixLabors.Fonts;

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

        var cooldown       = new Dictionary<long, DateTime>();
        var cooldownGlobal = DateTime.MinValue;

        var openTimes  = new Dictionary<string, int>();
        var rightTimes = new Dictionary<string, int>();
        var wrongTimes = new Dictionary<string, int>();

        string GetStatistic()
        {
            var openStr  = "开的次数：\n" + string.Join('\n', openTimes.OrderByDescending(x => x.Value).Select(x => $"{x.Key}: {x.Value}"));
            var rightStr = "答对次数：\n" + string.Join('\n', rightTimes.OrderByDescending(x => x.Value).Select(x => $"{x.Key}: {x.Value}"));
            var wrongStr = "答错次数：\n" + string.Join('\n', wrongTimes.OrderByDescending(x => x.Value).Select(x => $"{x.Key}: {x.Value}"));
            return $"{openStr}\n{rightStr}\n{wrongStr}";
        }

        var consolas = new Font(SystemFonts.Get("Consolas"), 22);

        string ReplyGenerator()
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

        MessageDataText ImageGenerator(string s)
        {
            return new MessageDataText(s);
            // var sd = new StringDrawer(5);
            // sd.Add(s, consolas, Color.Black);
            // var measure = sd.Measure();
            //
            // var image = new Image<Rgba32>((int)measure.Width, (int)measure.Height);
            // image.Clear(Color.White);
            // sd.Draw(image);
            // return MessageDataImage.FromBase64(image.ToB64());
        }

        var res = Dialog.AddHandler(message.GroupInfo?.Id, null, mNext =>
        {
            if (mNext.Command.StartsWith("开") && mNext.Command.Length == 2)
            {
                if (!regex.IsMatch(mNext.Command[1..]))
                {
                    return Task.FromResult(MarisaPluginTaskState.NoResponse);
                }

                if (DateTime.Now - cooldownGlobal < TimeSpan.FromMinutes(1))
                {
                    // mNext.Reply("你们开太快了");
                    return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
                }

                if (cooldown.TryGetValue(mNext.Sender!.Id, out var t))
                {
                    if (DateTime.Now - t < TimeSpan.FromMinutes(3))
                    {
                        // mNext.Reply("你开太快了");
                        return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
                    }
                }
                
                if (tips.Contains(mNext.Command[1]))
                {
                    return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
                }

                cooldown[mNext.Sender!.Id] = DateTime.Now;
                cooldownGlobal             = DateTime.Now;

                openTimes[mNext.Sender.Name] = openTimes.TryGetValue(mNext.Sender.Name, out var ot) ? ot + 1 : 1;
                tips.Add(char.ToLower(mNext.Command[1]));
                tips.Add(char.ToUpper(mNext.Command[1]));
                mNext.Reply(ImageGenerator(ReplyGenerator()), false);
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
                    // mNext.Reply("歌名长度错误");
                    wrongTimes[mNext.Sender!.Name] = wrongTimes.TryGetValue(mNext.Sender.Name, out var wt) ? wt + 1 : 1;
                }
                else
                {
                    if (name.Equals(songName[num - 1], StringComparison.OrdinalIgnoreCase))
                    {
                        if (!right.Contains(num - 1))
                        {
                            right.Add(num - 1);

                            rightTimes[mNext.Sender!.Name] = rightTimes.TryGetValue(mNext.Sender.Name, out var rt) ? rt + 1 : 1;

                            if (right.Count == songName.Count)
                            {
                                mNext.Reply($"全部猜出来了耶！", false);
                                // mNext.Reply(ImageGenerator(GetStatistic()), false);
                                return Task.FromResult(MarisaPluginTaskState.CompletedTask);
                            }

                            // mNext.Reply(ImageGenerator(ReplyGenerator()), false);
                            mNext.Reply("对对对");
                        }
                    }
                    else
                    {
                        // mNext.Reply("错错错");
                        wrongTimes[mNext.Sender!.Name] = wrongTimes.TryGetValue(mNext.Sender.Name, out var wt) ? wt + 1 : 1;
                    }
                }
            }

            return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
        });

        if (res)
        {
            message.Reply(ImageGenerator($"猜歌游戏开始！\n{ReplyGenerator()}发送“开`任意字符`”\n发送“`序号`:`歌名`”"), false);
        }
        else
        {
            message.Reply("？");
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    [GeneratedRegex("^[a-zA-Z0-9,./?():;'\"*!@#$%^&-_=+`~<> ]+$")]
    private static partial Regex SongTitleMatcher();
}
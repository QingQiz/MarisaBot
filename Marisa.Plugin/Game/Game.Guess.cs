using System.Diagnostics;
using System.Dynamic;
using System.Text;
using System.Text.RegularExpressions;
using Flurl.Http;
using Marisa.BotDriver.DI;
using Marisa.Plugin.Shared.Chunithm;
using Marisa.Plugin.Shared.Dialog;
using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.Util;
using Marisa.Plugin.Shared.Util.SongDb;
using Newtonsoft.Json;
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

    [MarisaPluginDoc("添加曲库，仅私聊可用", "`曲库名字`")]
    [MarisaPluginSubCommand(nameof(Guess))]
    [MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, "add")]
    private MarisaPluginTaskState GuessAddDb(Message message, long qq)
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

        DialogManager.TryAddDialog((message.GroupInfo?.Id, message.Sender.Id), mNext =>
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

            foreach (var title in titles)
                res.Add(title);
            mNext.Reply("继续", false);

            return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
        }, this);

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("一种新的猜歌游戏，仅群聊可用", "`数据库名`，可写多个，用`:`分隔")]
    [MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, "guess")]
    private MarisaPluginTaskState Guess(Message message, DictionaryProvider provider)
    {
        if (message.GroupInfo == null) return MarisaPluginTaskState.CompletedTask;

        // 兼容旧格式 :game guess friberg maimai，转发给 friberg
        var first = message.Command.Trim().ToString();
        if (first.StartsWith("friberg", StringComparison.OrdinalIgnoreCase) || first.StartsWith("弗一把"))
        {
            var prefixLen = first.StartsWith("friberg", StringComparison.OrdinalIgnoreCase) ? "friberg".Length : "弗一把".Length;
            var rest = message.Command.Trim().Slice(prefixLen).TrimStart();
            return GuessFriberg(message with { Command = rest }, provider);
        }

        if (!ReadTitles(message, out var songName, out var marisaPluginTaskState)) return marisaPluginTaskState;

        Debug.Assert(songName != null, nameof(songName) + " != null");

        var tips  = new HashSet<char>();
        var right = new HashSet<int>();

        var cooldown       = new Dictionary<long, DateTime>();
        var cooldownGlobal = DateTime.MinValue;

        var res = DialogManager.TryAddDialog((message.GroupInfo?.Id, null), mNext =>
        {
            if (mNext.Command.Span is "结束游戏")
            {
                return Task.FromResult(MarisaPluginTaskState.CompletedTask);
            }

            if (mNext.Command.StartsWith("开") && mNext.Command.Length == 2)
            {
                if (!SongTitleMatcher().IsMatch(mNext.Command[1..].ToString()))
                {
                    mNext.Reply("无效字符");
                    return Task.FromResult(MarisaPluginTaskState.NoResponse);
                }

                if (DateTime.Now - cooldownGlobal < TimeSpan.FromMinutes(1))
                {
                    mNext.Reply("冷却中...");
                    return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
                }

                if (cooldown.TryGetValue(mNext.Sender.Id, out var t))
                {
                    if (DateTime.Now - t < TimeSpan.FromMinutes(3))
                    {
                        mNext.Reply("冷却中...");
                        return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
                    }
                }

                if (tips.Contains(mNext.Command.Span[1]))
                {
                    mNext.Reply("？");
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
                    mNext.Reply("不对不对！");
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
        }, this);

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

    #region Friberg

    private static readonly string[] MaiVersionOrder =
    [
        "maimai", "maimai PLUS", "maimai GreeN", "maimai GreeN PLUS", "maimai ORANGE",
        "maimai ORANGE PLUS", "maimai PiNK", "maimai PiNK PLUS", "maimai MURASAKi",
        "maimai MURASAKi PLUS", "maimai MiLK", "MiLK PLUS", "maimai FiNALE",
        "maimai でらっくす", "maimai でらっくす Splash", "maimai でらっくす UNiVERSE",
        "maimai でらっくす FESTiVAL", "maimai でらっくす BUDDiES",
        "maimai でらっくす PRiSM", "maimai でらっくす PRiSM PLUS"
    ];

    private static readonly string[] ChuVersionOrder =
    [
        "CHUNITHM", "CHUNITHM PLUS", "CHUNITHM AIR", "CHUNITHM AIR PLUS",
        "CHUNITHM STAR", "CHUNITHM STAR PLUS", "CHUNITHM AMAZON", "CHUNITHM AMAZON PLUS",
        "CHUNITHM CRYSTAL", "CHUNITHM CRYSTAL PLUS", "CHUNITHM PARADISE",
        "CHUNITHM PARADISE LOST", "CHUNITHM NEW!!", "CHUNITHM NEW PLUS!!",
        "CHUNITHM SUN", "CHUNITHM SUN PLUS", "CHUNITHM LUMINOUS", "CHUNITHM LUMINOUS PLUS",
        "CHUNITHM VERSE", "CHUNITHM XVERSE", "CHUNITHM XVERSEX"
    ];

    private const double ConstantNear = 0.3;
    private const double BpmNear = 10;
    private const int VersionNear = 2;

    /// <summary>
    ///     各难度猜测次数上限：初级 6、中级 8、上级/超上级 10
    /// </summary>
    private static int FribergMaxTries(int lv)
    {
        return lv switch
        {
            0 => 6,
            1 => 8,
            _ => 10
        };
    }

    private static readonly Func<int, Func<List<Song>>> FribergDbReader = idx => idx switch
    {
        0 => () =>
        {
            var data = "https://www.diving-fish.com/api/maimaidxprober/music_data".GetJsonListAsync().Result;
            return data.Select(d => (Song)new MaiMaiSong(d)).ToList();
        },
        1 => () =>
        {
            var data =
                JsonConvert.DeserializeObject<ExpandoObject[]>(
                    File.ReadAllText(ResourceManager.ResourcePath + "/SongInfo.json")) as dynamic[];
            return data!.Select(d => (Song)new ChunithmSong(d)).ToList();
        },
        _ => throw new ArgumentOutOfRangeException(nameof(idx), idx, null)
    };
    [MarisaPluginDoc("friberg 猜歌游戏，仅群聊可用", "`guess friberg 数据库名`，可写多个，用`:`分隔")]
    [MarisaPluginCommand(StringComparison.OrdinalIgnoreCase, "friberg", "弗一把")]
    private MarisaPluginTaskState GuessFriberg(Message message, DictionaryProvider provider)
    {
        if (message.GroupInfo == null) return MarisaPluginTaskState.CompletedTask;

        var botQq = (long)provider["QQ"];

        var dbNames = message.Command.Split(':').Select(x => x.Trim()).Where(x => !x.IsEmpty)
            .Select(x => x.ToString() switch
            {
                "舞萌"     => "maimai",
                "中二"     => "chunithm",
                var other  => other
            }).ToArray();

        if (dbNames.Length == 0 || dbNames.Any(x => x != "maimai" && x != "chunithm"))
        {
            message.Reply("friberg 仅支持 maimai / chunithm 数据库");
            return MarisaPluginTaskState.CompletedTask;
        }

        var songs = new List<Song>();
        var aliases = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < GuessDbName.Count; i++)
        {
            if (dbNames.Contains(GuessDbName[i], StringComparer.OrdinalIgnoreCase))
            {
                songs.AddRange(FribergDbReader(i)());
                LoadAliases(aliases, GuessDbName[i]);
            }
        }

        songs = songs.Where(s => FribergInfo(s).Constant > 0).ToList();
        if (songs.Count == 0)
        {
            message.Reply("曲库为空");
            return MarisaPluginTaskState.CompletedTask;
        }

        var hostId = message.Sender.Id;
        var waitingDifficulty = true;
        Song? answer = null;
        var difficulty = 3;
        var tries = 0;
        var rows = new List<object>();
        var game = dbNames.First() == "maimai" ? "maimai" : "chunithm";

        async Task<MessageDataImage> Render()
        {
            var ctx = new WebContext();
            ctx.Put("FribergGame", game);
            ctx.Put("FribergRows", rows);
            ctx.Put("FribergTries", new { Tries = tries, Max = FribergMaxTries(difficulty) });
            return MessageDataImage.FromBase64(await WebApi.Friberg(ctx.Id));
        }

        var pendingList = new List<Song>();

        var res = DialogManager.TryAddDialog((message.GroupInfo?.Id, null), async mNext =>
        {
            // 难度选择阶段：只有开局者可回复数字序号（无需 @bot），0123 以外结束游戏
            if (waitingDifficulty)
            {
                if (mNext.Sender.Id != hostId)
                {
                    return MarisaPluginTaskState.NoResponse;
                }

                var choice = mNext.Command.Trim().ToString();

                if (choice == "结束游戏")
                {
                    mNext.Reply("游戏结束", false);
                    return MarisaPluginTaskState.CompletedTask;
                }

                if (int.TryParse(choice, out var lv) && lv is >= 0 and <= 3)
                {
                    var filtered = FilterByDifficulty(songs, lv);
                    if (filtered.Count == 0)
                    {
                        mNext.Reply("该难度下没有歌曲，游戏结束", false);
                        return MarisaPluginTaskState.CompletedTask;
                    }

                    answer = filtered.RandomTake();
                    waitingDifficulty = false;
                    difficulty = lv;
                    tries = FribergMaxTries(lv);

                    mNext.Reply($"难度已选择，猜歌开始！\n@魔理沙发送歌名进行猜测，共 {tries} 次机会\n@魔理沙发送\"结束游戏\"结束游戏", false);
                    return MarisaPluginTaskState.ToBeContinued;
                }

                mNext.Reply("不存在的选项，游戏结束", false);
                return MarisaPluginTaskState.CompletedTask;
            }

            // 只有 @bot 的消息才参与游戏，其余交给其它插件
            if (!mNext.IsAt(botQq))
            {
                return MarisaPluginTaskState.NoResponse;
            }

            // 在候选列表状态下，只有回复列表内 id 才走选择逻辑；否则清空列表，把当前消息当作新指令处理
            if (pendingList.Count != 0)
            {
                if (long.TryParse(mNext.Command.Trim().Span, out var id))
                {
                    var selected = pendingList.FirstOrDefault(s => s.Id == id);
                    if (selected != null)
                    {
                        pendingList.Clear();
                        return await GuessAndReply(selected);
                    }
                }

                pendingList.Clear();
            }

            if (mNext.Command.Span is "结束游戏")
            {
                rows.Add(CompareRow(answer!, answer!));
                mNext.Reply(await Render());
                mNext.Reply($"游戏结束！答案是：{answer!.Title}", false);
                return MarisaPluginTaskState.CompletedTask;
            }

            var input = mNext.Command.Trim();
            if (input.IsWhiteSpace())
            {
                mNext.Reply("@魔理沙发送歌名进行猜测");
                return MarisaPluginTaskState.ToBeContinued;
            }

            var matches = SearchSongs(songs, aliases, input);
            if (matches.Count == 0)
            {
                mNext.Reply("曲库里没有这首歌");
                return MarisaPluginTaskState.ToBeContinued;
            }

            if (matches.Count == 1)
            {
                return await GuessAndReply(matches[0]);
            }

            // 多个候选：回复列表，等待玩家回 id
            pendingList = matches;
            mNext.Reply(string.Join('\n', matches.Select(s => $"[ID:{s.Id}, Lv:{s.MaxLevel()}] -> {s.Title}")) + "\n发送歌曲 id 进一步选择");

            return MarisaPluginTaskState.ToBeContinued;

            async Task<MarisaPluginTaskState> GuessAndReply(Song guess)
            {
                if (guess.Title.Equals(answer!.Title, StringComparison.OrdinalIgnoreCase))
                {
                    rows.Add(CompareRow(answer, answer));
                    mNext.Reply(await Render());
                    mNext.Reply($"猜对了！答案是：{answer.Title}", false);
                    return MarisaPluginTaskState.CompletedTask;
                }

                rows.Add(CompareRow(guess, answer));
                tries--;
                mNext.Reply(await Render());

                if (tries <= 0)
                {
                    rows.Add(CompareRow(answer, answer));
                    mNext.Reply(await Render());
                    mNext.Reply($"次数用完了！答案是：{answer.Title}", false);
                    return MarisaPluginTaskState.CompletedTask;
                }

                return MarisaPluginTaskState.ToBeContinued;
            }
        }, this);

        if (res)
        {
            message.Reply(
                "friberg 猜歌游戏开始！\n" +
                "请选择难度（回复数字序号）：\n" +
                "0. 初级（紫谱定数 >= 14）\n" +
                "1. 中级（>= 13+）\n" +
                "2. 上级（>= 13）\n" +
                "3. 超上级（无限制）",
                false
            );
        }
        else
        {
            message.Reply("？");
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    private static List<Song> FilterByDifficulty(List<Song> songs, int lv)
    {
        return lv switch
        {
            0 => songs.Where(s => FribergInfo(s).Constant >= 14.0).ToList(),
            1 => songs.Where(s => FribergInfo(s).Constant >= 13.5).ToList(),
            2 => songs.Where(s => FribergInfo(s).Constant >= 13.0).ToList(),
            _ => songs.ToList()
        };
    }

    /// <summary>
    ///     仿 maisong (SongDb.SearchSong) 的搜索：id 筛选 → 精确 → 别名包含 → 正则 → 包含
    /// </summary>
    private static List<Song> SearchSongs(List<Song> songs, IReadOnlyDictionary<string, List<string>> aliases, ReadOnlyMemory<char> input)
    {
        var keyword = input.Trim().ToString();
        if (string.IsNullOrWhiteSpace(keyword)) return [];

        // id 筛选：纯数字 / id1111
        if (long.TryParse(keyword, out var id))
        {
            return songs.Where(s => s.Id == id).ToList();
        }
        if (keyword.StartsWith("id", StringComparison.OrdinalIgnoreCase) &&
            long.TryParse(keyword[2..].Trim(), out var songId))
        {
            return songs.Where(s => s.Id == songId).ToList();
        }

        // 1. 精确命中歌名
        var exact = songs.Where(s => s.Title.Equals(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count != 0) return exact;

        // 2. 别名包含匹配（仿 SongDb.SearchSongByAlias）
        var aliasTitles = aliases
            .Where(kv => kv.Key.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .SelectMany(kv => kv.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (aliasTitles.Count != 0)
        {
            return songs.Where(s => aliasTitles.Contains(s.Title, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        // 3. 正则匹配歌名
        try
        {
            var regex = new Regex(keyword, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200));
            var res = songs.Where(s => regex.IsMatch(s.Title)).ToList();
            if (res.Count != 0) return res;
        }
        catch (RegexParseException)
        {
        }

        // 4. 包含匹配歌名
        return songs
            .Where(s => s.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    ///     读取 aliases.tsv 构建 别名 -> 真实歌名列表 映射（格式同 SongDb.GetSongAliases，TSV 第一列为真实歌名）
    /// </summary>
    private static void LoadAliases(Dictionary<string, List<string>> aliases, string dbName)
    {
        var path = dbName == "maimai"
            ? Marisa.Plugin.Shared.MaiMaiDx.ResourceManager.ResourcePath + "/aliases.tsv"
            : ResourceManager.ResourcePath + "/aliases.tsv";

        try
        {
            foreach (var line in File.ReadAllLines(path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var titles = line
                    .Split('\t')
                    .Select(x => x.AsMemory().Trim().UnEscapeTsvCell().ToString())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                if (titles.Count < 2) continue;

                foreach (var alias in titles.Skip(1))
                {
                    if (!aliases.TryGetValue(alias, out var list))
                    {
                        list = [];
                        aliases[alias] = list;
                    }
                    list.Add(titles[0]);
                }
            }
        }
        catch (IOException)
        {
        }
    }

    private static object CompareRow(Song guess, Song answer)
    {
        var (gTitle, gArtist, gGenre, gVersion, gConstant, gBpm) = FribergInfo(guess);
        var (aTitle, aArtist, aGenre, aVersion, aConstant, aBpm) = FribergInfo(answer);

        return new
        {
            Title = CompareCell(gTitle, aTitle),
            Artist = CompareCell(gArtist, aArtist),
            Genre = CompareCell(gGenre, aGenre),
            Version = CompareVersion(gVersion, aVersion),
            Constant = CompareNear(gConstant, aConstant, ConstantNear),
            Bpm = CompareNear(gBpm, aBpm, BpmNear),
            Extra = CompareExtra(guess, answer)
        };
    }

    private static object CompareCell(string guess, string answer)
    {
        return new
        {
            Value = guess,
            Status = guess.Equals(answer, StringComparison.OrdinalIgnoreCase) ? "correct" : "wrong",
            Arrow = ""
        };
    }

    private static object CompareVersion(string guess, string answer)
    {
        var gIdx = Array.IndexOf(MaiVersionOrder, guess);
        var aIdx = Array.IndexOf(MaiVersionOrder, answer);
        var isMai = gIdx != -1 || aIdx != -1;

        if (!isMai)
        {
            gIdx = Array.IndexOf(ChuVersionOrder, guess);
            aIdx = Array.IndexOf(ChuVersionOrder, answer);
        }

        var display = ShortVersion(guess);

        if (gIdx == -1 || aIdx == -1)
        {
            return new { Value = display, Status = "wrong", Arrow = "" };
        }

        if (gIdx == aIdx)
        {
            return new { Value = display, Status = "correct", Arrow = "" };
        }

        // ← 正确答案版本比猜测歌曲早；→ 反之代表晚
        var arrow = aIdx < gIdx ? "←" : "→";
        if (Math.Abs(gIdx - aIdx) > VersionNear)
        {
            return new { Value = display, Status = "wrong", Arrow = arrow };
        }

        return new { Value = display, Status = "near", Arrow = arrow };
    }

    private static object CompareNear(double guess, double answer, double near)
    {
        // ↑ 正确答案对应值大于猜测歌曲；↓ 反之
        var arrow = answer > guess ? "↑" : "↓";

        if (guess.Equals(answer))
        {
            return new { Value = guess, Status = "correct", Arrow = "" };
        }

        if (Math.Abs(guess - answer) > near)
        {
            return new { Value = guess, Status = "wrong", Arrow = arrow };
        }

        return new { Value = guess, Status = "near", Arrow = arrow };
    }

    /// <summary>
    ///     版本显示名：舞萌 DX 系列用国服命名（舞萌DX 202x），其余去掉 maimai / CHUNITHM 前缀
    /// </summary>
    private static string ShortVersion(string version)
    {
        if (version == "CHUNITHM") return "無印";
        if (version.StartsWith("CHUNITHM ")) return version["CHUNITHM ".Length..];

        if (version == "maimai") return "無印";
        if (version.StartsWith("maimai ")) version = version["maimai ".Length..];

        return version switch
        {
            "でらっくす"            => "舞萌DX",
            "でらっくす Splash"     => "舞萌DX 2020",
            "でらっくす UNiVERSE"   => "舞萌DX 2021",
            "でらっくす FESTiVAL"   => "舞萌DX 2022",
            "でらっくす BUDDiES"    => "舞萌DX 2023",
            "でらっくす PRiSM"      => "舞萌DX 2024",
            "でらっくす PRiSM PLUS" => "舞萌DX 2025",
            _                      => version
        };
    }

    private static object CompareExtra(Song guess, Song answer)
    {
        var gIdx = GetExtraIdx(guess);
        var aIdx = GetExtraIdx(answer);

        var gHas = gIdx >= 0;
        var aHas = aIdx >= 0;

        // 答案有该谱面且选项也有：显示选项定数，相等绿、不等黄
        if (aHas && gHas)
        {
            var gc = guess.Constants[gIdx];
            var ac = answer.Constants[aIdx];

            return new
            {
                Value = gc.ToString("F1"),
                Status = Math.Abs(gc - ac) < 0.0001 ? "correct" : "near",
                Arrow = ""
            };
        }

        return new
        {
            Value = gHas ? "有" : "无",
            Status = gHas == aHas ? "correct" : "wrong",
            Arrow = ""
        };
    }

    private static int GetExtraIdx(Song song)
    {
        return song.DiffNames.FindIndex(x =>
            x.Equals("Re:Master", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("ULTIMA", StringComparison.OrdinalIgnoreCase));
    }

    private static (string Title, string Artist, string Genre, string Version, double Constant, double Bpm) FribergInfo(Song song)
    {
        return song switch
        {
            MaiMaiSong mai => (
                mai.Title, mai.Artist, mai.Info.Genre, mai.Info.From,
                GetConstant(mai), mai.Info.Bpm
            ),
            ChunithmSong chu => (
                chu.Title, chu.Artist, chu.Genre, chu.Version,
                GetConstant(chu), GetBpm(chu)
            ),
            _ => (song.Title, song.Artist, "", song.Version, GetConstant(song), song.Bpm)
        };
    }

    private static double GetConstant(Song song)
    {
        var idx = song.DiffNames.FindIndex(x => x.Equals("MASTER", StringComparison.OrdinalIgnoreCase));
        return idx >= 0 && idx < song.Constants.Count ? song.Constants[idx] : 0;
    }

    private static double GetBpm(ChunithmSong song)
    {
        var idx = song.DiffNames.FindIndex(x => x.Equals("MASTER", StringComparison.OrdinalIgnoreCase));
        if (idx < 0 || idx >= song.BpmList.Count) return 0;
        return song.BpmList[idx].FirstOrDefault();
    }

    #endregion
}

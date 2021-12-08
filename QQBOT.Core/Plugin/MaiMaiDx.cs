using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Flurl.Http;
using Newtonsoft.Json;
using QQBOT.Core.Attribute;
using QQBOT.Core.MiraiHttp;
using QQBOT.Core.MiraiHttp.Entity;
using QQBOT.Core.Plugin.PluginEntity;
using QQBOT.Core.Plugin.PluginEntity.MaiMaiDx;
using QQBOT.Core.Util;
using QQBOT.EntityFrameworkCore;
using QQBOT.EntityFrameworkCore.Entity.Plugin.MaiMaiDx;

namespace QQBOT.Core.Plugin
{
    [MiraiPlugin(priority:1)]
    public class MaiMaiDx : PluginBase
    {
        #region B40

        private static async Task<DxRating> MaiB40(object sender)
        {

            var response = await "https://www.diving-fish.com/api/maimaidxprober/query/player".PostJsonAsync(sender);
            var json     = await response.GetJsonAsync();

            var rating = new DxRating(json);

            return rating;
        }

        #endregion

        #region Search

        private List<MaiMaiSong> _songList;
        private Dictionary<string, List<string>> _songAlias;

        private List<MaiMaiSong> SongList
        {
            get
            {
                if (_songList == null)
                {
                    try
                    {
                        var data = "https://www.diving-fish.com/api/maimaidxprober/music_data"
                            .GetJsonListAsync()
                            .Result;

                        _songList = new List<MaiMaiSong>();
                        foreach (var d in data)
                        {
                            _songList.Add(new MaiMaiSong(d));
                        }
                    }
                    catch
                    {
                        var data = JsonConvert.DeserializeObject<ExpandoObject[]>(
                            File.ReadAllText(ResourceManager.ResourcePath + "/SongInfo.json")
                        ) as dynamic[];
                        _songList = new List<MaiMaiSong>();
                        foreach (var d in data)
                        {
                            _songList.Add(new MaiMaiSong(d));
                        }
                    }
                }

                return _songList;
            }
        }

        private Dictionary<string, List<string>> SongAlias
        {
            get
            {
                if (_songAlias == null)
                {
                    _songAlias = new Dictionary<string, List<string>>();

                    foreach (var song in SongList)
                    {
                        if (!_songAlias.ContainsKey(song.Title))
                        {
                            _songAlias[song.Title] = new List<string>();
                        }
                        _songAlias[song.Title].Add(song.Title);
                    }

                    foreach (var line in File.ReadAllLines(ResourceManager.ResourcePath + "/aliases.tsv"))
                    {
                        var titles = line
                            .Split('\t')
                            .Select(x => x.Trim().Trim('"').Replace("\"\"", "\""))
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .ToList();

                        foreach (var title in titles)
                        {
                            if (!_songAlias.ContainsKey(title))
                            {
                                _songAlias[title] = new List<string>();
                            }

                            _songAlias[title].Add(titles[0]);
                        }
                    }
                }

                return _songAlias;
            }
        }

        private MessageChain GetSongInfo(long songId)
        {
            var searchResult = SongList.FirstOrDefault(song => song.Id == songId);

            if (searchResult == null)
            {
                return MessageChain.FromPlainText($"“未找到 ID 为 {songId} 的歌曲”");
            }

            return new MessageChain(new MessageData[]
            {
                new PlainMessage(searchResult.Title),
                ImageMessage.FromBase64(searchResult.GetImage())
            });
        }

        private List<MaiMaiSong> SearchSong(string alias)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                return null;
            }

            return SongAlias.Keys
                .Where(songNameAlias => songNameAlias.Contains(alias, StringComparison.OrdinalIgnoreCase))
                .SelectMany(songNameAlias => SongAlias[songNameAlias]/*song name*/)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(songName => SongList.Any(song => string.Equals(song.Title, songName, StringComparison.OrdinalIgnoreCase)))
                .Select(songName =>
                    SongList.First(song => string.Equals(song.Title, songName, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        private List<MaiMaiSong> ListSongs(string param)
        {
            if (string.IsNullOrEmpty(param))
            {
                return SongList;
            }

            string[] subCommand =
            //      0       1          2        3      4
                { "base", "level", "charter", "bpm", "lv"};
            var res = param.CheckPrefix(subCommand).ToList();

            if (!res.Any()) return new List<MaiMaiSong>();

            var (prefix, index) = res.First();

            switch (index)
            {
                case 0: // base
                {
                    var param1 = param.TrimStart(prefix).Trim();

                    if (param1.Contains('-'))
                    {
                        if (double.TryParse(param1.Split('-')[0], out var @base1) &&
                            double.TryParse(param1.Split('-')[1], out var @base2))
                        {
                            return SongList.Where(s => s.Constants.Any(b => b >= base1 && b <= base2)).ToList();
                        }
                    }
                    else
                    {
                        if (double.TryParse(param1, out var @base))
                        {
                            return SongList.Where(s => s.Constants.Contains(@base)).ToList();
                        }
                    }
                    return new List<MaiMaiSong>();
                }
                case 4:
                case 1: // level
                {
                    var lv = param.TrimStart(prefix).Trim();
                    return SongList.Where(s => s.Levels.Contains(lv)).ToList();
                }
                case 2: // charter
                {
                    var charter = param.TrimStart(prefix).Trim();
                    return SongList
                        .Where(s => s.Charts
                            .Any(c => c.Charter.Contains(charter, StringComparison.OrdinalIgnoreCase)))
                        .ToList();
                }
                case 3: // bpm
                {
                    var param1 = param.TrimStart(prefix).Trim();

                    if (param1.Contains('-'))
                    {
                        if (long.TryParse(param1.Split('-')[0], out var bpm1) &&
                            long.TryParse(param1.Split('-')[1], out var bpm2))
                        {
                            return SongList.Where(s => s.Info.Bpm >= bpm1 && s.Info.Bpm <= bpm2).ToList();
                        }
                    }
                    else
                    {
                        if (long.TryParse(param1, out var bpm))
                        {
                            return SongList.Where(s => s.Info.Bpm == bpm).ToList();
                        }
                    }
                    return new List<MaiMaiSong>();
                }
            }

            return new List<MaiMaiSong>();
        }

        private static MessageChain GetSearchResult(IReadOnlyList<MaiMaiSong> songs)
        {
            if (songs == null)
            {
                return MessageChain.FromPlainText("啥？");
            }

            return songs.Count switch
            {
                >= 10 => MessageChain.FromPlainText($"过多的结果（{songs.Count}个）"),
                0 => MessageChain.FromPlainText("“查无此歌”"),
                1 => new MessageChain(new MessageData[]
                {
                    new PlainMessage(songs[0].Title),
                    ImageMessage.FromBase64(songs[0].GetImage())
                }),
                _ => MessageChain.FromPlainText(string.Join('\n', songs.Select(song => $"[T:{song.Type}, ID:{song.Id}] -> {song.Title}")))
            };
        }

        /// <summary>
        /// </summary>
        /// <param name="song"></param>
        /// <returns>plain text</returns>
        private static MessageChain GetSearchResultS(IReadOnlyList<MaiMaiSong> song)
        {
            if (song == null)
            {
                return MessageChain.FromPlainText("啥？");
            }

            return song.Count switch
            {
                >= 30 => MessageChain.FromPlainText($"过多的结果（{song.Count}个）"),
                0 => MessageChain.FromPlainText("“查无此歌”"),
                1 => MessageChain.FromPlainText($"Title: {song[0].Title}\nArtist: {song[0].Info.Artist}"),
                _ => MessageChain.FromPlainText(string.Join('\n', song.Select(s => $"[T:{s.Type}, ID:{s.Id}] -> {s.Title}")))
            };
        }

        #endregion

        #region guess

        private static HashSet<long> _songIdWithWave;

        private static async Task<PluginTaskState> ProcSongGuessResult(MiraiHttpSession session, Message msg, MaiMaiSong song,
            MaiMaiSong guess)
        {
            var dbContext = new BotDbContext();

            var @new      = dbContext.MaiMaiDxGuesses.Any(u => u.UId == msg.Sender.Id);
            var u = @new
                ? dbContext.MaiMaiDxGuesses.First(u => u.UId == msg.Sender.Id)
                : new MaiMaiDxGuess(msg.Sender!.Id, msg.Sender!.Name);

            // 未知的歌，不算
            if (guess == null)
            {
                await session.SendGroupMessage(
                    new Message(MessageChain.FromPlainText("没找到你说的这首歌")), msg.GroupInfo!.Id, msg.Source!.Id);
                return PluginTaskState.ToBeContinued;
            }

            // 猜对了
            if (guess.Title == song.Title)
            {
                u.TimesCorrect++;
                u.Name = msg.Sender!.Name;
                dbContext.MaiMaiDxGuesses.InsertOrUpdate(u);
                await dbContext.SaveChangesAsync();

                await session.SendGroupMessage(new Message(new MessageData[]
                {
                    new PlainMessage($"你猜对了！正确答案：{song.Title}"),
                    ImageMessage.FromBase64(song.GetImage()),
                }), msg.GroupInfo!.Id, msg.Source!.Id);

                return PluginTaskState.CompletedTask;
            }

            // 猜错了
            u.TimesWrong++;
            u.Name = msg.Sender!.Name;
            dbContext.MaiMaiDxGuesses.InsertOrUpdate(u);
            await dbContext.SaveChangesAsync();

            await session.SendGroupMessage(
                new Message(MessageChain.FromPlainText("不对不对！")), msg.GroupInfo!.Id, msg.Source!.Id);
            return PluginTaskState.ToBeContinued;
        }

        private Func<MiraiHttpSession, Message, Task<PluginTaskState>> GenGuessDialogHandler(MaiMaiSong song, long groupId, DateTime startTime)
        {
            return async (session, msg) =>
            {
                var m = msg.MessageChain!.PlainText.Trim();

                switch (m)
                {
                    case "结束猜曲" or "答案":
                    {
                        await session.SendGroupMessage(new Message(new MessageData[]
                        {
                            new PlainMessage($"猜曲结束，正确答案：{song.Title}"),
                            ImageMessage.FromBase64(song.GetImage()),
                            new PlainMessage(
                                $"当前歌在录的别名有：{string.Join(", ", GetSongAliasesByName(song.Title))}\n若有遗漏，请联系作者"),
                        }), groupId);
                        return PluginTaskState.CompletedTask;
                    }
                    case "来点提示":
                        switch (new Random().Next(3))
                        {
                            case 0:
                                await session.SendGroupMessage(
                                    new Message(MessageChain.FromPlainText($"作曲家是：{song.Info.Artist}")), groupId);
                                return PluginTaskState.ToBeContinued;
                            case 1:
                                await session.SendGroupMessage(
                                    new Message(MessageChain.FromPlainText($"是个{song.Levels.Last()}")), groupId);
                                return PluginTaskState.ToBeContinued;
                            case 2:
                            {
                                var cover = ResourceManager.GetCover(song.Id, false);
                                
                                await session.SendGroupMessage(new Message(new MessageData[]
                                {
                                    new PlainMessage("封面裁剪："),
                                    ImageMessage.FromBase64(cover.RandomCut(cover.Width / 3, cover.Height / 3).ToB64())
                                }), groupId);
                                return PluginTaskState.ToBeContinued;
                                
                            }
                            case 3:
                                await session.SendGroupMessage(
                                    new Message(MessageChain.FromPlainText($"歌曲类别是：{song.Info.Genre}")), groupId);
                                return PluginTaskState.ToBeContinued;
                        }

                        break;
                }

                var procResult =
                    new Func<MaiMaiSong, Task<PluginTaskState>>((MaiMaiSong s) =>
                        ProcSongGuessResult(session, msg, song, s));

                if (!msg.At(session.Id))
                {
                    // continue
                    if (DateTime.Now - startTime <= TimeSpan.FromMinutes(10)) return PluginTaskState.NoResponse;

                    // time out
                    await session.SendGroupMessage(new Message(MessageChain.FromPlainText("舞萌猜曲已结束")), groupId);
                    return PluginTaskState.CompletedTask;
                }

                var search = SearchSong(m);

                if (long.TryParse(m, out var id))
                {
                    search.AddRange(SongList.Where(s => s.Id == id));
                }

                if (m.StartsWith("id", StringComparison.OrdinalIgnoreCase))
                {
                    if (long.TryParse(m.TrimStart("id").Trim(), out var songId))
                    {
                        search = SongList.Where(s => s.Id == songId).ToList();
                    }
                }

                switch (search.Count)
                {
                    case 0:
                        return await procResult(null);
                    case 1:
                        return await procResult(search[0]);
                    default:
                        await session.SendGroupMessage(new Message(GetSearchResult(search)), groupId, msg.Source!.Id);
                        return PluginTaskState.ToBeContinued;
                }
            };
        }

        private MessageChain StartGuess(long groupId, long senderId, string senderName, MaiMaiSong song)
        {
            var res = Dialog.AddHandler(groupId, 
                (session, msg) => GenGuessDialogHandler(song, groupId, DateTime.Now)(session, msg));

            if (!res)
            {
                return MessageChain.FromPlainText("？");
            }

            using var dbContext = new BotDbContext();

            if (dbContext.MaiMaiDxGuesses.Any(g => g.UId == senderId))
            {
                var g = dbContext.MaiMaiDxGuesses.First(g => g.UId == senderId);
                g.Name       =  senderName;
                g.TimesStart += 1;
                dbContext.Update(g);
            }
            else
            {
                dbContext.MaiMaiDxGuesses.Add(new MaiMaiDxGuess(senderId, senderName)
                {
                    TimesStart = 1
                });
            }
            dbContext.SaveChanges();
            return null;
        }

        private IEnumerable<MessageChain> StartSongSoundGuess(Message message)
        {
            const string songPath = @"C:\Users\sofee\Desktop\maifinale";

            // init song list if needed
            _songIdWithWave ??= Directory.GetFiles(songPath, "*.wav")
                .Select(Path.GetFileName)
                .Select(fn => long.Parse(fn.TrimStart('0')[..^4]))
                .ToHashSet();
            
            var groupId = message.GroupInfo!.Id;

            // random cut song
            var song = SongList.Where(s => _songIdWithWave.Contains(s.Id)).ToList().RandomTake();

            // add handler
            // 这里应该先加 handler 再剪曲子
            var mc = StartGuess(groupId, message.Sender!.Id, message.Sender!.Name, song);

            if (mc == null)
            {
                var wav = new WavFileExt(Path.Join(songPath, song.Id.ToString("000") + ".wav"));
                var sec = (int)wav.TotalSecond.TotalSeconds;

                var start = new Random().Next(sec - 12);

                var cutVidPath = $@"D:\MaiMaiDx\song_guess_cut_{groupId}.wav";
                var outStream  = new FileStream(cutVidPath, FileMode.Create);

                wav.TrimWav(outStream, TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(15));

                // convert WAV to AMR
                // 我他妈服了，傻逼QQ用的这个AMR编码，这质量tm烂成啥样了。。。
                using (var p = new Process())
                {
                    p.StartInfo.UseShellExecute        = false;
                    p.StartInfo.CreateNoWindow         = true;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.FileName               = @"C:\bin\ffmpeg.exe";
                    p.StartInfo.Arguments =
                        $"-i {cutVidPath} -loglevel quiet -y -ar 8000 -ac 1 -ab 12.2k {cutVidPath}.amr";
                    p.Start();
                    p.WaitForExit();
                }

                var bytes  = File.ReadAllBytes(cutVidPath + ".amr");
                var toSend = Convert.ToBase64String(bytes);

                yield return new MessageChain(new MessageData[]
                {
                    new PlainMessage("听歌猜曲模式启动！"),
                    new PlainMessage("艾特我+你的答案以参加猜曲\n答案可以是 `歌曲名`、`歌曲id` 或 `id歌曲id`\n\n发送 ”结束猜曲“ 来退出猜曲模式"),
                });

                yield return new MessageChain(new MessageData[]
                {
                    VoiceMessage.FromBase64(toSend)
                });
            }
            else
            {
                yield return mc;
            }
        }

        private MessageChain StartSongCoverGuess(Message message)
        {
            var groupId = message.GroupInfo!.Id;

            var song = SongList.RandomTake();
            
            var cover = ResourceManager.GetCover(song.Id, false);

            var cw = cover.Width  / 3;
            var ch = cover.Height / 3;

            var mc = StartGuess(groupId, message.Sender!.Id, message.Sender!.Name, song);

            if (mc == null)
            {
                return new MessageChain(new MessageData[]
                {
                    new PlainMessage("猜曲模式启动！"),
                    ImageMessage.FromBase64(cover.RandomCut(cw, ch).ToB64()),
                    new PlainMessage("艾特我+你的答案以参加猜曲\n答案可以是 `歌曲名`、`歌曲id` 或 `id歌曲id`\n\n发送 ”结束猜曲“ 来退出猜曲模式"),
                });
            }
            else
            {
                // 你妈的，不能删这个 else，万一哪天改成 yield return 这就成了一个隐藏的大黑锅
                return mc;
            }
        }

        private IEnumerable<MessageChain> SongGuessMessageHandler(Message message, string param)
        {
            if (message.GroupInfo == null)
                yield return MessageChain.FromPlainText("仅群组中使用");

            switch (param)
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

                    if (!res.Any()) yield return MessageChain.FromPlainText("None");

                    yield return MessageChain.FromPlainText(string.Join('\n',
                        res.Select((guess, i) =>
                            $"{i + 1}、 {guess.Name}： (s:{guess.TimesStart}, w:{guess.TimesWrong}, c:{guess.TimesCorrect})")));
                    break;
                }
                case "":
                    yield return StartSongCoverGuess(message);
                    break;
                case "v2":
                {
                    foreach (var res in StartSongSoundGuess(message))
                    {
                        yield return res;
                    }
                    break;
                }
            }

            yield return null;
        }

        #endregion

        #region Alias

        private List<string> GetSongAliasesByName(string name)
        {
            var aliases = SongAlias
                .Where(k /* alias: [song title] */ => k.Value.Contains(name))
                .Select(k => k.Key);
            return aliases.ToList();
        }

        private MessageChain SongAliasHandler(string param)
        {
            string[] subCommand =
            //      0      1
                { "get", "set", };
            
            var res = param.CheckPrefix(subCommand).ToList();

            var (prefix, index) = res.First();

            switch (index)
            {
                case 0:
                {
                    var songName = param.TrimStart(prefix).Trim();

                    if (string.IsNullOrEmpty(songName))
                    {
                        return MessageChain.FromPlainText("？");
                    }

                    var songList = SearchSong(songName);

                    if (songList.Count == 1)
                    {
                        return MessageChain.FromPlainText(
                            $"当前歌在录的别名有：{string.Join(", ", GetSongAliasesByName(songList[0].Title))}");
                    }
                    return GetSearchResult(songList);
                }
                case 1:
                {
                    var param2 = param.TrimStart(prefix).Trim();
                    var names = param2.Split("$>");

                    if (names.Length != 2)
                    {
                        return MessageChain.FromPlainText("Failed");
                    }

                    lock (SongAlias)
                    {
                        var name  = names[0].Trim();
                        var alias = names[1].Trim();

                        if (SongList.Any(song => song.Title == name))
                        {
                            File.AppendAllText(@"D:\MaiMaiDx\MaiMaiSongAliasTemp.txt", $"\n{name}\t{alias}");

                            if (SongAlias.ContainsKey(alias))
                            {
                                SongAlias[alias].Add(name);
                            }
                            else
                            {
                                SongAlias[alias] = new List<string> { name };
                            }

                            return MessageChain.FromPlainText("Success");
                        }

                        return MessageChain.FromPlainText($"不存在的歌曲：{name}");
                    }
                }
            }
            
            return null;
        }
        

        #endregion
        
        #region Message Handler

        private async IAsyncEnumerable<MessageChain> Handler(Message message)
        {
            string[] commandPrefix = { "maimai", "mai", "舞萌" };
            string[] subCommand =
                {
                //    0       1        2      3       4      5       6      7      8        9       10      11     12
                    "b40", "search", "搜索", "查分", "搜歌", "song", "查歌", "id", "name", "random", "随机", "随歌", "list",
                //    13      14     15      16
                    "rand", "猜歌", "猜曲", "alias"
                };

            var sender = message.Sender;
            var msg    = message.MessageChain!.PlainText.Trim().TrimStart(commandPrefix);

            
            if (string.IsNullOrEmpty(msg)) yield return null;

            var prefixes = msg.CheckPrefix(subCommand);

            foreach (var (prefix, index) in prefixes)
            {
                switch (index)
                {
                    case 0:
                    case 3: // b40
                        var username = msg.TrimStart(prefix).Trim();

                        MessageChain ret;
                        try
                        {
                            var rating = await MaiB40(string.IsNullOrEmpty(username)
                                ? new { qq = sender!.Id }
                                : new { username });

                            var imgB64 = rating.GetImage();


                            ret = new MessageChain(new[]
                            {
                                ImageMessage.FromBase64(imgB64)
                            });
                        }
                        catch (FlurlHttpException e) when (e.StatusCode == 400)
                        {
                            ret = MessageChain.FromPlainText("“查无此人”");
                        }
                        catch (FlurlHttpException e) when (e.StatusCode == 403)
                        {
                            ret = MessageChain.FromPlainText("“403 forbidden”");
                        }
                        catch (FlurlHttpException e)
                        {
                            ret = MessageChain.FromPlainText($"BOT在差你分的过程中炸了：\n{e}");
                        }

                        yield return ret;
                        break;
                    case 1:
                    case 2:
                    case 4:
                    case 5:
                    case 6: // search
                    {
                        var name   = msg.TrimStart(prefix).Trim();
                        var search = SearchSong(name);
                        yield return GetSearchResult(search);
                        break;
                    }
                    case 7: // id
                    {
                        var last = msg.TrimStart(prefix).Trim();

                        yield return long.TryParse(last, out var id)
                            ? GetSongInfo(id)
                            : MessageChain.FromPlainText("“你看你输的这个几把玩意儿像不像个ID”");
                        break;
                    }
                    case 8: // name
                    {
                        var name   = msg.TrimStart(prefix).Trim();
                        var search = SearchSong(name);
                        yield return GetSearchResultS(search);
                        break;
                    }
                    case 9:
                    case 10:
                    case 11:
                    case 13: // random
                    {
                        var param = msg.TrimStart(prefix).Trim();
                        var list  = ListSongs(param);

                        yield return list.Count == 0
                            ? MessageChain.FromPlainText("“NULL”")
                            : MessageChain.FromBase64(list[new Random().Next(list.Count)].GetImage());
                        break;
                    }
                    case 12: // list
                    {
                        var param = msg.TrimStart(prefix).Trim();
                        var list  = ListSongs(param);
                        var rand  = new Random();

                        if (list.Count == 0)
                        {
                            yield return MessageChain.FromPlainText("“EMPTY”");
                            break;
                        }

                        var str = string.Join('\n',
                            list.OrderBy(_ => rand.Next())
                                .Take(15)
                                .OrderBy(x => x.Id)
                                .Select(song => $"[T:{song.Type}, ID:{song.Id}] -> {song.Title}"));

                        if (list.Count > 15)
                        {
                            str += "\n" + $"太多了（{list.Count}），随机给出15个";
                        }

                        yield return MessageChain.FromPlainText(str);
                        break;
                    }
                    case 14:
                    case 15: // 猜歌
                    {
                        var param = msg.TrimStart(prefix).Trim();

                        foreach (var res in SongGuessMessageHandler(message, param))
                        {
                            yield return res;
                        }
                        break;
                    }
                    case 16: // alias
                    {
                        var param = msg.TrimStart(prefix).Trim();
                        yield return SongAliasHandler(param);
                        break;
                    }
                }
            }

            yield return null;
        }

        private async Task<IAsyncEnumerable<MessageChain>> HandlerWrapper(MiraiHttpSession session, Message message)
        {
            try
            {
                return Handler(message);
            }
            catch (Exception e)
            {
                if (message.GroupInfo == null)
                {
                    await session.SendFriendMessage(
                        new Message(MessageChain.FromPlainText(e.ToString())),
                        message.Sender!.Id);
                }
                else
                {
                    await session.SendGroupMessage(
                        new Message(MessageChain.FromPlainText(e.ToString())),
                        message.GroupInfo!.Id);
                }
                throw;
            }
        }

        protected override async Task<PluginTaskState> FriendMessageHandler(MiraiHttpSession session, Message message)
        {
            var mc  = await HandlerWrapper(session, message);

            if (mc == null) return PluginTaskState.NoResponse;

            var proceed = false;

            await foreach (var m in mc.WithCancellation(default).ConfigureAwait(false))
            {
                if (m == null) break;
                proceed = true;
                await session.SendFriendMessage(new Message(m), message.Sender!.Id);
            }
            return proceed ? PluginTaskState.CompletedTask : PluginTaskState.NoResponse;
        }

        protected override async Task<PluginTaskState> GroupMessageHandler(MiraiHttpSession session, Message message)
        {
            var mc  = await HandlerWrapper(session, message);

            if (mc == null) return PluginTaskState.NoResponse;

            var source = message.Source.Id;

            var proceed = false;

            await foreach (var m in mc.WithCancellation(default).ConfigureAwait(false))
            {
                if (m == null) break;
                proceed = true;
                await session.SendGroupMessage(new Message(m), message.GroupInfo!.Id, source);
            }
            return proceed ? PluginTaskState.CompletedTask : PluginTaskState.NoResponse;
        }

        #endregion
    }
}
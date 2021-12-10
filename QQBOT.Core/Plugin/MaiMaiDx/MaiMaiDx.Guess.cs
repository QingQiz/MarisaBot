using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using QQBOT.Core.MiraiHttp;
using QQBOT.Core.MiraiHttp.Entity;
using QQBOT.Core.Plugin.PluginEntity;
using QQBOT.Core.Plugin.PluginEntity.MaiMaiDx;
using QQBOT.Core.Util;
using QQBOT.EntityFrameworkCore;
using QQBOT.EntityFrameworkCore.Entity.Plugin.MaiMaiDx;

namespace QQBOT.Core.Plugin.MaiMaiDx
{
    public partial class MaiMaiDx
    {
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

                var procResult =
                    new Func<MaiMaiSong, Task<PluginTaskState>>(s => ProcSongGuessResult(session, msg, song, s));

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
            var songPath = ConfigurationManager.AppSettings["MaiMaiDx.WaveFilePath"] ?? string.Empty;

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

                var cutVidPath = ResourceManager.TempPath + $@"/song_guess_cut_{groupId}.wav";
                var outStream  = new FileStream(cutVidPath, FileMode.Create);

                wav.TrimWav(outStream, TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(15));

                // convert WAV to AMR
                // 我他妈服了，傻逼QQ用的这个AMR编码，这质量tm烂成啥样了。。。
                using (var p = new Process())
                {
                    p.StartInfo.UseShellExecute        = false;
                    p.StartInfo.CreateNoWindow         = true;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.FileName               = ConfigurationManager.AppSettings["FFMPEG.Path"] ?? string.Empty;
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
    }
}
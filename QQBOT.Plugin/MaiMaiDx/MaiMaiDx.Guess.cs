using System.Configuration;
using System.Diagnostics;
using System.Text.RegularExpressions;
using QQBot.EntityFrameworkCore;
using QQBot.EntityFrameworkCore.Entity.Plugin.MaiMaiDx;
using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Entity.MessageData;
using QQBot.MiraiHttp.Plugin;
using QQBot.Plugin.Shared.MaiMaiDx;
using QQBot.Plugin.Shared.Util;

namespace QQBot.Plugin.MaiMaiDx;

public partial class MaiMaiDx
{
    private static HashSet<long>? _songIdWithWave;

    private static async Task<MiraiPluginTaskState> ProcSongGuessResult(MessageSenderProvider sender, Message message, MaiMaiSong song, MaiMaiSong? guess)
    {
        var dbContext  = new BotDbContext();
        var senderId   = message.Sender!.Id;
        var senderName = message.Sender!.Name;

        var @new = dbContext.MaiMaiDxGuesses.Any(u => u.UId == senderId);
        var u = @new
            ? dbContext.MaiMaiDxGuesses.First(u => u.UId == senderId)
            : new MaiMaiDxGuess(senderId, senderName);

        // 未知的歌，不算
        if (guess == null)
        {
            sender.SendByRecv(MessageChain.FromPlainText("没找到你说的这首歌"), message);
            return MiraiPluginTaskState.ToBeContinued;
        }

        // 猜对了
        if (guess.Title == song.Title)
        {
            u.TimesCorrect++;
            u.Name = senderName;
            dbContext.MaiMaiDxGuesses.InsertOrUpdate(u);
            await dbContext.SaveChangesAsync();

            sender.Reply(new MessageChain(new MessageData[]
            {
                new PlainMessage($"你猜对了！正确答案：{song.Title}"),
                ImageMessage.FromBase64(song.GetImage())
            }), message);

            return MiraiPluginTaskState.CompletedTask;
        }

        // 猜错了
        u.TimesWrong++;
        u.Name = senderName;
        dbContext.MaiMaiDxGuesses.InsertOrUpdate(u);
        await dbContext.SaveChangesAsync();

        sender.SendByRecv( MessageChain.FromPlainText("不对不对！"), message);
        return MiraiPluginTaskState.ToBeContinued;
    }

    private Func<MessageSenderProvider, Message, Task<MiraiPluginTaskState>> GenGuessDialogHandler(MaiMaiSong song,
        DateTime startTime, long qq)
    {
        return async (ms, message) =>
        {
            switch (message.Command)
            {
                case "结束猜曲" or "答案":
                {
                    ms.SendByRecv(new MessageChain(new MessageData[]
                    {
                        new PlainMessage($"猜曲结束，正确答案：{song.Title}"),
                        ImageMessage.FromBase64(song.GetImage()),
                        new PlainMessage(
                            $"当前歌在录的别名有：{string.Join(", ", GetSongAliasesByName(song.Title))}\n若有遗漏，请联系作者")
                    }), message);
                    return MiraiPluginTaskState.CompletedTask;
                }
                case "来点提示":
                {
                    MessageChain? hint = null;
                    switch (new Random().Next(3))
                    {
                        case 0:
                            hint = MessageChain.FromPlainText($"作曲家是：{song.Info.Artist}");
                            break;
                        case 1:
                            hint = MessageChain.FromPlainText($"是个{song.Levels.Last()}");
                            break;
                        case 2:
                        {
                            var cover = ResourceManager.GetCover(song.Id, false);

                            hint = new MessageChain(new MessageData[]
                            {
                                new PlainMessage("封面裁剪："),
                                ImageMessage.FromBase64(cover.RandomCut(cover.Width / 3, cover.Height / 3).ToB64())
                            });
                            break;
                        }
                        case 3:
                            hint = MessageChain.FromPlainText($"歌曲类别是：{song.Info.Genre}");
                            break;
                    }

                    ms.SendByRecv(hint!, message, false);

                    return MiraiPluginTaskState.ToBeContinued;
                }
            }

            if (!message.At(qq))
            {
                // continue
                if (DateTime.Now - startTime <= TimeSpan.FromMinutes(5)) return MiraiPluginTaskState.NoResponse;

                // time out
                ms.SendByRecv(MessageChain.FromPlainText("舞萌猜曲已结束"), message, false);
                return MiraiPluginTaskState.Canceled;
            }

            var search = SearchSong(message.Command);

            var procResult =
                new Func<MaiMaiSong?, Task<MiraiPluginTaskState>>(s => ProcSongGuessResult(ms, message, song, s));

            switch (search.Count)
            {
                case 0:
                    return await procResult(null);
                case 1:
                    return await procResult(search[0]);
                default:
                    ms.SendByRecv(GetSearchResult(search), message);
                    return MiraiPluginTaskState.ToBeContinued;
            }
        };
    }

    private bool StartGuess(MaiMaiSong song, MessageSenderProvider ms, Message message, long qq)
    {
        var senderId   = message.Sender!.Id;
        var senderName = message.Sender!.Name;
        var groupId    = message.GroupInfo!.Id;
        var now        = DateTime.Now;
        var res = Dialog.AddHandler(groupId,
            (sender, msg) => GenGuessDialogHandler(song, now, qq)(sender, msg));

        if (!res)
        {
            ms.SendByRecv(MessageChain.FromPlainText("？"), message);
            return false;
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

        return true;
    }

    private void StartSongSoundGuess(Message message, MessageSenderProvider ms, long qq)
    {
        var songPath = ConfigurationManager.AppSettings["MaiMaiDx.WaveFilePath"] ?? string.Empty;

        // init song list if needed
        _songIdWithWave ??= Directory.GetFiles(songPath, "*.wav")
            .Select(Path.GetFileName)
            .Select(fn => long.Parse(fn?.TrimStart('0')[..^4] ?? "-1"))
            .Where(x => x != -1)
            .ToHashSet();

        var groupId = message.GroupInfo!.Id;

        // random cut song
        var song = SongList.Where(s => _songIdWithWave.Contains(s.Id)).ToList().RandomTake();

        // ReSharper disable once InvertIf
        if (StartGuess(song, ms, message, qq))
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

            ms.SendByRecv(new MessageChain(new MessageData[]
            {
                new PlainMessage("听歌猜曲模式启动！"),
                new PlainMessage("艾特我+你的答案以参加猜曲\n答案可以是 `歌曲名`、`歌曲id` 或 `id歌曲id`\n\n发送 ”结束猜曲“ 来退出猜曲模式")
            }), message);

            ms.SendByRecv(MessageChain.FromVoiceB64(toSend), message, false);
        }
    }

    private void StartSongCoverGuess(Message message, MessageSenderProvider ms, long qq, Regex? categoryFilter)
    {
        var songs = SongList.Where(s => categoryFilter?.IsMatch(s.Info.Genre) ?? true)
            .ToList();

        if (!songs.Any())
        {
            ms.SendByRecv(MessageChain.FromPlainText("None"), message);
            return;
        }

        var song = songs.RandomTake();

        var cover = ResourceManager.GetCover(song.Id, false);

        var cw = cover.Width  / 3;
        var ch = cover.Height / 3;

        if (StartGuess(song, ms, message, qq))
        {
            ms.SendByRecv(new MessageChain(new MessageData[]
            {
                new PlainMessage("猜曲模式启动！"),
                ImageMessage.FromBase64(cover.RandomCut(cw, ch).ToB64()),
                new PlainMessage("艾特我+你的答案以参加猜曲\n答案可以是 `歌曲名`、`歌曲id` 或 `id歌曲id`\n\n发送 ”结束猜曲“ 来退出猜曲模式")
            }), message);
        }
    }
}
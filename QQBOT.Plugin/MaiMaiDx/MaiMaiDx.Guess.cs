using System.Configuration;
using System.Diagnostics;
using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Entity.MessageData;
using QQBot.Plugin.Shared.MaiMaiDx;
using QQBot.Plugin.Shared.Util;

namespace QQBot.Plugin.MaiMaiDx;

public partial class MaiMaiDx
{
    private static HashSet<long>? _songIdWithWave;

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
        var song = _songDb.SongList.Where(s => _songIdWithWave.Contains(s.Id)).ToList().RandomTake();

        // ReSharper disable once InvertIf
        if (_songDb.StartGuess(song, ms, message, qq))
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

            ms.Reply(new MessageChain(new MessageData[]
            {
                new PlainMessage("听歌猜曲模式启动！"),
                new PlainMessage("艾特我+你的答案以参加猜曲\n答案可以是 `歌曲名`、`歌曲id` 或 `id歌曲id`\n\n发送 ”结束猜曲“ 来退出猜曲模式")
            }), message);

            ms.Reply(MessageChain.FromVoiceB64(toSend), message, false);
        }
    }
}
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
    private void StartSongSoundGuess(Message message, MessageSenderProvider ms, long qq)
    {
        const int duration = 15;

        var groupId = message.GroupInfo!.Id;

        // select a song
        var song = _songDb.SongList.RandomTake();

        // ReSharper disable once InvertIf
        if (_songDb.StartGuess(song, ms, message, qq))
        {
            var songPath =
                Directory.GetDirectories(ConfigurationManager.AppSettings["MaiMaiDx.BeatMap"] ?? string.Empty,
                    $"{song.Id}_*", SearchOption.AllDirectories).First();
            songPath = Path.Join(songPath, "track.mp3");
            
            var tag = TagLib.File.Create(songPath);
            var d   = tag.Properties.Duration;
            var start = new Random().Next((int)d.TotalSeconds - duration);
            

            var cutVidPath = ResourceManager.TempPath + $@"/song_guess_cut_{groupId}";

            // random cut and convert to .amr
            // 我他妈服了，傻逼QQ用的这个AMR编码，这质量tm烂成啥样了。。。
            using (var p = new Process())
            {
                p.StartInfo.UseShellExecute        = false;
                p.StartInfo.CreateNoWindow         = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.FileName               = ConfigurationManager.AppSettings["FFMPEG.Path"] ?? string.Empty;
                p.StartInfo.Arguments =
                    $"-i \"{songPath}\" -ss {start} -t {duration} -y -ar 8000 -ac 1 -ab 12.2k {cutVidPath}.amr";
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
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
    private static List<MaiMaiSong>? _songWithFile;

    private static readonly Dictionary<string, (string Path, TimeSpan Duration)> SongPath = new();

    private void StartSongSoundGuess(Message message, MessageSenderProvider ms, long qq)
    {
        lock ("SongDb")
        {
            if (!SongPath.Any())
            {
                var path = ConfigurationManager.AppSettings["MaiMaiDx.SongPath"] ?? string.Empty;

                var files = Directory.GetFiles(path, "*.mp3", SearchOption.AllDirectories);

                Parallel.ForEach(files, f =>
                {
                    var tag   = TagLib.File.Create(f);
                    var title = tag.Tag.Title;
                    var d     = tag.Properties.Duration;

                    lock ("SongPath") SongPath[title] = (f, d);
                });
            }
        }

        const int duration = 15;

        // init song list if needed
        _songWithFile ??= _songDb.SongList.Where(s => SongPath.ContainsKey(s.Title)).ToList();

        var groupId = message.GroupInfo!.Id;

        // select a song
        var song = _songWithFile.RandomTake();

        // ReSharper disable once InvertIf
        if (_songDb.StartGuess(song, ms, message, qq))
        {
            var sp    = SongPath[song.Title];
            var start = new Random().Next((int)sp.Duration.TotalSeconds - duration);

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
                    $"-i \"{sp.Path}\" -ss {start} -t {duration} -y -ar 8000 -ac 1 -ab 12.2k {cutVidPath}.amr";
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
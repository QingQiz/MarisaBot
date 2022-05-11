﻿using System.Diagnostics;

namespace Marisa.Plugin.Arcaea;

public partial class Arcaea
{
    private void StartSongSoundGuess(Message message, long qq)
    {
        const int duration = 15;

        var groupId = message.GroupInfo!.Id;

        // select a song
        var song = _songDb.SongList
            .Where(s => string.Compare(s.Version, "3.6.1.1", StringComparison.Ordinal) <= 0)
            .ToList()
            .RandomTake();

        // ReSharper disable once InvertIf
        if (_songDb.StartGuess(song, message, qq))
        {
            var songPath = ConfigurationManager.Configuration.Arcaea.AssetsPath;
            var songId   = song.CoverFileName.Replace("_byd", "").Replace(".jpg", "").Replace(".png", "");

            songPath = Path.Join(songPath, "songs", songId, "base.ogg");

            var tag   = TagLib.File.Create(songPath);
            var d     = tag.Properties.Duration;
            var start = new Random().Next((int)d.TotalSeconds - duration);

            var cutVidPath = ConfigurationManager.Configuration.Arcaea.TempPath;
            cutVidPath = Path.Join(cutVidPath, $"/song_guess_cut_{groupId}");

            // random cut and convert to .amr
            // 我他妈服了，傻逼QQ用的这个AMR编码，这质量tm烂成啥样了。。。
            using (var p = new Process())
            {
                p.StartInfo.UseShellExecute        = false;
                p.StartInfo.CreateNoWindow         = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.FileName               = ConfigurationManager.Configuration.FfmpegPath;
                p.StartInfo.Arguments =
                    $"-i \"{songPath}\" -ss {start} -t {duration} -y -ar 8000 -ac 1 -ab 12.2k {cutVidPath}.amr";
                p.Start();
                p.WaitForExit();
            }

            var bytes  = File.ReadAllBytes(cutVidPath + ".amr");
            var toSend = Convert.ToBase64String(bytes);

            message.Reply(
                new MessageDataText("听歌猜曲模式启动！"),
                new MessageDataText("艾特我+你的答案以参加猜曲\n答案可以是 `歌曲名`、`歌曲id` 或 `id歌曲id`\n\n发送 ”结束猜曲“ 来退出猜曲模式")
            );

            message.Reply(MessageDataVoice.FromBase64(toSend), false);
        }
    }
}
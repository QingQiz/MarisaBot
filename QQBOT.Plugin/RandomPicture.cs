using System.Drawing;
using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Entity.MessageData;
using QQBot.MiraiHttp.Plugin;
using QQBot.Plugin.Shared.Util;

namespace QQBot.Plugin;

[MiraiPluginCommand("抽图")]
public class RandomPicture: MiraiPluginBase
{
    private static readonly List<string> ImageList = new ();
    private static FileSystemWatcher? _imageListWatcher;

    [MiraiPluginCommand(strict: true)]
    private MiraiPluginTaskState Handler(Message m, MessageSenderProvider p)
    {
        if (_imageListWatcher == null)
        {
            _imageListWatcher = new FileSystemWatcher
            {
                Path         = @"C:\Users\sofee\Desktop\pic",
                NotifyFilter = NotifyFilters.LastWrite,
                Filter       = "*.*"
            };

            _imageListWatcher.Changed += (_, _) =>
            {
                lock (ImageList)
                {
                    Thread.Sleep(500);
                    ImageList.Clear();
                }
            };
            _imageListWatcher.EnableRaisingEvents = true;
            GC.KeepAlive(_imageListWatcher);
        }

        lock (ImageList)
        {
            if (ImageList.Count == 0)
            {
                ImageList.AddRange(Directory
                    .GetFiles(@"C:\Users\sofee\Desktop\pic", "*.*", SearchOption.AllDirectories)
                    .Where(fn => fn.EndsWith("jpg") || fn.EndsWith("jpeg") || fn.EndsWith("png")));
            }

            p.Reply(ImageMessage.FromBase64((Image.FromFile(ImageList.RandomTake()) as Bitmap)!.ToB64()), m, false);

            return MiraiPluginTaskState.CompletedTask;
        }
    }
}
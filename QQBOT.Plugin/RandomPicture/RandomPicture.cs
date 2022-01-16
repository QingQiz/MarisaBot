using System.Drawing;
using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Entity.MessageData;
using QQBot.MiraiHttp.Plugin;
using QQBot.Plugin.Shared.Util;

namespace QQBot.Plugin.RandomPicture;

[MiraiPluginCommand("抽图", "ct")]
[MiraiPluginTrigger(typeof(MiraiPluginTrigger), nameof(MiraiPluginTrigger.PlainTextTrigger))]
public class RandomPicture : MiraiPluginBase
{
    private static readonly List<string> PicDbPath = new()
    {
        @"C:\Users\sofee\Desktop\pic"
    };

    private static readonly List<string> PicDbPathExclude = new()
    {
        "R18", "backup"
    };

    private static readonly List<string> AvailableFileExt= new()
    {
        "jpg", "png", "jpeg"
    };

    private static readonly List<string> ImageList = new();

    [MiraiPluginCommand(true, "")]
    private static MiraiPluginTaskState Handler(Message m, MessageSenderProvider p)
    {
        lock (ImageList)
        {
            if (ImageList.Count == 0)
            {
                foreach (var path in PicDbPath)
                {
                    ImageList.AddRange(Directory
                        .GetFiles(path, "*.*", SearchOption.AllDirectories)
                        .Where(fn => PicDbPathExclude.All(ex => !fn.Contains(ex, StringComparison.OrdinalIgnoreCase)))
                        .Where(fn =>
                            AvailableFileExt.Any(ext => fn.EndsWith(ext, StringComparison.OrdinalIgnoreCase))));
                }
            }

            var pic = ImageList.RandomTake();

            p.Reply(Path.GetFileName(pic), m);
            p.Reply(ImageMessage.FromPath(pic), m, false);

            return MiraiPluginTaskState.CompletedTask;
        }
    }

    [MiraiPluginCommand(true, "reset")]
    private static MiraiPluginTaskState Reset(Message m, MessageSenderProvider p)
    {
        lock (ImageList)
        {
            const long authorId = 642191352L;
            var        sender   = m.Sender!.Id;

            if (sender == authorId)
            {
                ImageList.Clear();
                p.Reply("Success", m);
                return MiraiPluginTaskState.CompletedTask;
            }
            else
            {
                p.Reply("Denied", m);
                return MiraiPluginTaskState.CompletedTask;
            }
        }
    }
}
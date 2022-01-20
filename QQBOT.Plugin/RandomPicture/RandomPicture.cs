using System.Configuration;
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
    private static readonly string PicDbPath = ConfigurationManager.AppSettings["PicDbPath"]!;

    private static readonly List<string> PicDbPathExclude = new()
    {
        "R18", "backup"
    };

    private static readonly List<string> AvailableFileExt = new()
    {
        "jpg", "png", "jpeg"
    };

    [MiraiPluginCommand(true, "")]
    private static MiraiPluginTaskState Handler(Message m, MessageSenderProvider p)
    {
        var imageList = Directory
            .GetFiles(PicDbPath, "*.*", SearchOption.AllDirectories)
            .Where(fn => PicDbPathExclude.All(ex => !fn.Contains(ex, StringComparison.OrdinalIgnoreCase)))
            .Where(fn =>
                AvailableFileExt.Any(ext => fn.EndsWith(ext, StringComparison.OrdinalIgnoreCase))).ToList();

        var pic = imageList.RandomTake();

        p.Reply(Path.GetFileName(pic), m);
        p.Reply(ImageMessage.FromPath(pic), m, false);

        return MiraiPluginTaskState.CompletedTask;
    }
}
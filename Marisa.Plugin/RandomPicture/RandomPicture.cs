using System.Configuration;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Plugin;
using Marisa.BotDriver.Plugin.Trigger;
using Marisa.Utils;

namespace Marisa.Plugin.RandomPicture;

[MarisaPluginCommand("抽图", "ct")]
[MarisaPluginTrigger(typeof(MarisaPluginTrigger), nameof(MarisaPluginTrigger.PlainTextTrigger))]
public class RandomPicture : MarisaPluginBase
{
    private static readonly string PicDbPath = ConfigurationManager.AppSettings["PicDbPath"]!;

    private static readonly List<string> PicDbPathExclude = new()
    {
        "R18", "backup", "看看"
    };

    private static readonly List<string> AvailableFileExt = new()
    {
        "jpg", "png", "jpeg"
    };

    [MarisaPluginCommand(true, "")]
    private static MarisaPluginTaskState Handler(Message m)
    {
        var imageList = Directory
            .GetFiles(PicDbPath, "*.*", SearchOption.AllDirectories)
            .Where(fn => PicDbPathExclude.All(ex => !fn.Contains(ex, StringComparison.OrdinalIgnoreCase)))
            .Where(fn =>
                AvailableFileExt.Any(ext => fn.EndsWith(ext, StringComparison.OrdinalIgnoreCase))).ToList();

        var pic = imageList.RandomTake();

        m.Reply(pic.Replace(PicDbPath, ""));
        m.Reply(MessageDataImage.FromPath(pic), false);

        return MarisaPluginTaskState.CompletedTask;
    }
}
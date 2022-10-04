namespace Marisa.Plugin.RandomPicture;

[MarisaPluginDoc("从作者图库里随机抽取一张图")]
[MarisaPluginCommand("抽图", "ct")]
[MarisaPluginTrigger(nameof(MarisaPluginTrigger.PlainTextTrigger))]
public class RandomPicture : MarisaPluginBase
{
    private static string PicDbPath => ConfigurationManager.Configuration.RandomPicture.ImageDatabasePath;

    private static IEnumerable<string> PicDbPathExclude =>
        ConfigurationManager.Configuration.RandomPicture.FileNameExclude;

    private static IEnumerable<string> AvailableFileExt =>
        ConfigurationManager.Configuration.RandomPicture.AvailableFileExt;

    [MarisaPluginCommand(true, "")]
    private static MarisaPluginTaskState Handler(Message m)
    {
        if (!Directory.Exists(PicDbPath))
        {
            return MarisaPluginTaskState.NoResponse;
        }

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
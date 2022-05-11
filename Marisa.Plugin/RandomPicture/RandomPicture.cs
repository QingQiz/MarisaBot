namespace Marisa.Plugin.RandomPicture;

[MarisaPluginCommand("抽图", "ct")]
[MarisaPluginTrigger(typeof(MarisaPluginTrigger), nameof(MarisaPluginTrigger.PlainTextTrigger))]
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
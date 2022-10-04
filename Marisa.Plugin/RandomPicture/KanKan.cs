namespace Marisa.Plugin.RandomPicture;

[MarisaPluginDoc("从作者图库里取出某人的图，例如：看看魔理沙")]
[MarisaPluginCommand("看看", "kk")]
[MarisaPluginTrigger(nameof(MarisaPluginTrigger.PlainTextTrigger))]
public class KanKan : MarisaPluginBase
{
    private static string PicDbPath => ConfigurationManager.Configuration.RandomPicture.ImageDatabaseKanKanPath;

    private static IEnumerable<string> PicDbPathExclude =>
        ConfigurationManager.Configuration.RandomPicture.FileNameExclude;

    private static IEnumerable<string> AvailableFileExt =>
        ConfigurationManager.Configuration.RandomPicture.AvailableFileExt;

    private static IEnumerable<string> Names
    {
        get
        {
            if (!Directory.Exists(PicDbPath))
            {
                return Enumerable.Empty<string>();
            }

            return Directory.GetDirectories(PicDbPath, "*", SearchOption.AllDirectories)
                .Where(d => PicDbPathExclude.All(e => !d.Contains(e)))
                .Select(d => d.TrimEnd('\\').TrimEnd('/'));
        }
    }

    private static List<string> GetImList(string name)
    {
        return Directory
            .GetFiles(name, "*.*", SearchOption.AllDirectories)
            .Where(fn => PicDbPathExclude.All(ex => !fn.Contains(ex, StringComparison.OrdinalIgnoreCase)))
            .Where(fn =>
                AvailableFileExt.Any(ext => fn.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    [MarisaPluginCommand]
    private static MarisaPluginTaskState Handler(Message m)
    {
        if (string.IsNullOrWhiteSpace(m.Command))
        {
            // m.Reply("看你妈，没有，爬！");

            return MarisaPluginTaskState.CompletedTask;
        }

        var names = Names.ToList();

        var n = names.FirstOrDefault(name => Path.GetFileName(name) == m.Command);

        if (n == null)
        {
            var alias = ConfigurationManager.Configuration.RandomPicture.Alias;
            n = names
                .Select(name => (name, Path.GetFileName(name)))
                .Where(name => alias.ContainsKey(name.Item2))
                .Where(name => alias[name.Item2].Contains(m.Command))
                .Select(name => name.Item1)
                .FirstOrDefault();

            if (n == null)
            {
                return MarisaPluginTaskState.NoResponse;
            }
        }

        var pic = GetImList(n).RandomTake();

        m.Reply(pic.Replace(PicDbPath, ""));
        m.Reply(MessageDataImage.FromPath(pic), false);

        return MarisaPluginTaskState.CompletedTask;
    }
}
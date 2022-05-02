using System.Configuration;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Plugin;
using Marisa.BotDriver.Plugin.Trigger;
using Marisa.Utils;

namespace Marisa.Plugin.RandomPicture;

[MarisaPluginCommand("看看", "kk")]
[MarisaPluginTrigger(typeof(MarisaPluginTrigger), nameof(MarisaPluginTrigger.PlainTextTrigger))]
public class KanKan : MarisaPluginBase
{
    private static readonly string PicDbPath = ConfigurationManager.AppSettings["PicDbPath_KanKan"]!;

    private static readonly List<string> PicDbPathExclude = new()
    {
        "R18", "backup"
    };

    private static readonly List<string> AvailableFileExt = new()
    {
        "jpg", "png", "jpeg"
    };

    private static IEnumerable<string?> Names =>
        Directory.GetDirectories(PicDbPath, "*", SearchOption.AllDirectories)
            .Where(d => PicDbPathExclude.All(e => !d.Contains(e)))
            .Select(d => d.TrimEnd('\\').TrimEnd('/'));

    private static readonly string[][] Alias =
    {
        new [] { "古明地恋", "恋" },
        new [] { "陈睿", "cr", "叔叔" },
        new [] { "初音未来", "miku" }
    };

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
        var n = m.Command;

        if (string.IsNullOrWhiteSpace(n))
        {
            if (new Random().Next(10) < 3)
            {
                m.Reply("看你妈，没有，爬！");
            }
            else
            {
                m.Reply(MessageDataImage.FromPath(Path.Join(ConfigurationManager.AppSettings["Help"]!, "kk.jpg")), false);
            }

            return MarisaPluginTaskState.CompletedTask;
        }

        n = Alias.FirstOrDefault(@as => @as.Any(a => a.Equals(n, StringComparison.OrdinalIgnoreCase)))?.First() ?? n;

        var d = Names.FirstOrDefault(d => Path.GetFileName(d)!.Equals(n, StringComparison.OrdinalIgnoreCase));

        if (d == null)
        {
            return MarisaPluginTaskState.NoResponse;
        }
        
        var pic = GetImList(d).RandomTake();

        m.Reply(pic.Replace(PicDbPath, ""));
        m.Reply(MessageDataImage.FromPath(pic), false);

        return MarisaPluginTaskState.CompletedTask;
    }
}
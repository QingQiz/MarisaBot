using System.Configuration;
using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Entity.MessageData;
using QQBot.MiraiHttp.Plugin;
using QQBot.Plugin.Shared.Util;

namespace QQBot.Plugin.RandomPicture;

[MiraiPluginCommand("看看", "kk")]
[MiraiPluginTrigger(typeof(MiraiPluginTrigger), nameof(MiraiPluginTrigger.PlainTextTrigger))]
public class KanKan : MiraiPluginBase
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

    [MiraiPluginCommand]
    private static MiraiPluginTaskState Handler(Message m, MessageSenderProvider p)
    {
        var n = m.Command;

        if (string.IsNullOrWhiteSpace(n))
        {
            if (new Random().Next(10) < 3)
            {
                p.Reply($"看你妈，没有，爬！", m);
            }
            else
            {
                p.Reply(ImageMessage.FromPath(Path.Join(ConfigurationManager.AppSettings["Help"]!, "kk.jpg")), m);
            }

            return MiraiPluginTaskState.CompletedTask;
        }

        n = Alias.FirstOrDefault(@as => @as.Any(a => a.Equals(n, StringComparison.OrdinalIgnoreCase)))?.First() ?? n;

        var d = Names.FirstOrDefault(d => Path.GetFileName(d)!.Equals(n, StringComparison.OrdinalIgnoreCase));

        if (d == null)
        {
            return MiraiPluginTaskState.NoResponse;
        }
        
        var pic = GetImList(d).RandomTake();

        p.Reply(pic.Replace(PicDbPath, ""), m);
        p.Reply(ImageMessage.FromPath(pic), m, false);

        return MiraiPluginTaskState.CompletedTask;
    }
}
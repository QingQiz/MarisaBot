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
        Directory.GetDirectories(PicDbPath, "*", SearchOption.TopDirectoryOnly)
            .Where(d => PicDbPathExclude.All(e => !d.Contains(e)))
            .Select(Path.GetFileName);

    private static readonly Dictionary<string, string> Alias = new()
    {
        { "ml", "Making＊Lovers" },
        { "DDLC", "Doki Doki Literature Club!" },
        { "lsg", "Love's Sweet Garnish" }
    };

    private static List<string> GetImList(string name)
    {
        return Directory
            .GetFiles(Path.Join(PicDbPath, name), "*.*", SearchOption.AllDirectories)
            .Where(fn => PicDbPathExclude.All(ex => !fn.Contains(ex, StringComparison.OrdinalIgnoreCase)))
            .Where(fn =>
                AvailableFileExt.Any(ext => fn.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    [MiraiPluginCommand]
    private static MiraiPluginTaskState Handler(Message m, MessageSenderProvider p)
    {
        if (Names.Contains(m.Command, StringComparer.OrdinalIgnoreCase))
        {
            var pic = GetImList(m.Command).RandomTake();

            p.Reply(pic.Replace(PicDbPath, ""), m);
            p.Reply(ImageMessage.FromPath(pic), m, false);

            return MiraiPluginTaskState.CompletedTask;
        }

        // ReSharper disable once InvertIf
        if (string.IsNullOrWhiteSpace(m.Command))
        {
            if (new Random().Next(10) < 3)
            {
                p.Reply($"看你妈，没有，爬！", m);
            }
            else
            {
                // var names   = string.Join('、', Names.Select(n => $"`{n}`"));
                // var aliases = Alias.Select(k => $"- `{k.Key}` 是指 `{k.Value}`");
                // p.Reply($"现在能看的只有：{names}\n\n这其中：\n{string.Join('\n', aliases)}", m);
                p.Reply(ImageMessage.FromPath(Path.Join(ConfigurationManager.AppSettings["Help"]!, "kk.jpg")), m);
            }

            return MiraiPluginTaskState.CompletedTask;
        }

        return MiraiPluginTaskState.NoResponse;
    }
}
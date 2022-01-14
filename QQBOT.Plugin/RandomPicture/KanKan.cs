using System.Drawing;
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
    private const string PicDbPath = @"C:\Users\sofee\Desktop\pic\看看";

    private static readonly List<string> PicDbPathExclude = new()
    {
        "R18"
    };

    private static readonly List<string> AvailableFileExt = new()
    {
        "jpg", "png", "jpeg"
    };

    private static readonly Dictionary<string, string> Alias = new()
    {
        { "恋爱是甜蜜调味料", "Love Sweet Garnish 2" },
        { "Love Sweet Garnish 2".ToLower(), "Love Sweet Garnish 2" },
        { "DDLC".ToLower(), "DDLC" },
        { "苍彼", "苍彼" },
        { "君彼女", "君彼女" },
        { "巧克甜恋", "巧克甜恋" },
        { "银河龙", "银河龙" },
        { "秋回", "秋回" },
    };

    private static List<string> GetImList(string name)
    {
        return Directory
            .GetFiles(Path.Join(PicDbPath, Alias[name]), "*.*", SearchOption.AllDirectories)
            .Where(fn => PicDbPathExclude.All(ex => !fn.Contains(ex, StringComparison.OrdinalIgnoreCase)))
            .Where(fn =>
                AvailableFileExt.Any(ext => fn.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    [MiraiPluginCommand]
    private static MiraiPluginTaskState Handler(Message m, MessageSenderProvider p)
    {
        if (Alias.ContainsKey(m.Command.ToLower()))
        {
            var pic = GetImList(m.Command.ToLower()).RandomTake();

            p.Reply(Path.GetFileName(pic), m);
            p.Reply(ImageMessage.FromPath(pic), m, false);
        }
        else
        {
            if (new Random().Next(10) < 3)
            {
                p.Reply($"看你妈，没有，爬！", m);
            }
            else
            {
                p.Reply($"现在能看的只有：{string.Join('、', Alias.Values.Distinct())}\n现在过滤了的有：{string.Join('、', PicDbPathExclude)}",
                    m, false);
            }
        }

        return MiraiPluginTaskState.CompletedTask;
    }
}
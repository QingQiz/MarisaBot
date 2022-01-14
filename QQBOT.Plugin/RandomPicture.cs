using System.Drawing;
using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Entity.MessageData;
using QQBot.MiraiHttp.Plugin;
using QQBot.Plugin.Shared.Util;

namespace QQBot.Plugin;

[MiraiPluginCommand("抽图", "ct")]
public class RandomPicture : MiraiPluginBase
{
    private static readonly List<string> ImageList = new();

    [MiraiPluginCommand(true, "")]
    private static MiraiPluginTaskState Handler(Message m, MessageSenderProvider p)
    {
        lock (ImageList)
        {
            if (ImageList.Count == 0)
            {
                ImageList.AddRange(Directory
                    .GetFiles(@"C:\Users\sofee\Desktop\pic", "*.*", SearchOption.AllDirectories)
                    .Where(fn => fn.EndsWith("jpg") || fn.EndsWith("jpeg") || fn.EndsWith("png")));
            }

            p.Reply(ImageMessage.FromBase64((Image.FromFile(ImageList.RandomTake()) as Bitmap)!.ToB64()), m, false);

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
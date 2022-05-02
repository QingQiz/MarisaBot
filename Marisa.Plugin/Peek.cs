using System.Drawing;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Plugin;
using Marisa.BotDriver.Plugin.Trigger;
using Marisa.Plugin.Shared.Util;

namespace Marisa.Plugin;

[MarisaPluginCommand(":peek")]
[MarisaPluginTrigger(typeof(MarisaPluginTrigger), nameof(MarisaPluginTrigger.PlainTextTrigger))]
public class Peek : MarisaPluginBase
{
    private bool _peekEnabled = true;

    private static string CaptureScreen(bool hide = true)
    {
        const int w = 1440;
        const int h = 810;

        var bitmap = new Bitmap(2560, 1440);

        using (var g = Graphics.FromImage(bitmap))
        {
            g.CopyFromScreen(0, 0, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
        }

        return hide
            ? bitmap.Resize(w, h).Blur(new Rectangle(0, 0, w, h), 4).ToB64()
            : bitmap.ToB64();
    }

    [MarisaPluginCommand]
    private MarisaPluginTaskState Handler(Message message)
    {
        const long authorId = 642191352L;
        var        senderId = message.Sender!.Id;

        MessageChain chain;
        switch (message.Command)
        {
            // disable peek
            case "0" when senderId == authorId:
                _peekEnabled = false;
                chain        = MessageChain.FromText("peek disabled");
                break;
            case "1" when senderId == authorId:
                _peekEnabled = true;
                chain        = MessageChain.FromText("peek enabled");
                break;
            case "" when _peekEnabled:
                chain = MessageChain.FromImageB64(CaptureScreen(senderId != authorId));
                break;
            default:
                chain = MessageChain.FromText("Denied");
                break;
        }
            
        message.Reply(chain);

        return MarisaPluginTaskState.CompletedTask;
    }
}
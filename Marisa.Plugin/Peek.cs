using System.Drawing;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Marisa.Plugin;

[MarisaPluginDoc("偷窥作者屏幕（？）")]
[MarisaPluginCommand(":peek")]
[MarisaPluginTrigger(nameof(MarisaPluginTrigger.PlainTextTrigger))]
public class Peek : MarisaPluginBase
{
    private bool _peekEnabled = true;

    private static string CaptureScreen(bool hide = true)
    {
        var bitmap = new Bitmap(2560, 1440);

        using (var g = Graphics.FromImage(bitmap))
        {
            g.CopyFromScreen(0, 0, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
        }

        var im = bitmap.ToImageSharpImage<Rgba32>();

        if (hide)
        {
            im.Mutate(i => i.Resize(0.5).GaussianBlur(3));
        }

        return im.ToB64();
    }

    [MarisaPluginCommand]
    private MarisaPluginTaskState Handler(Message message)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            message.Reply("暂时停用的功能");
            return MarisaPluginTaskState.CompletedTask;
        }

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
                chain = MessageChain.FromText("哒咩哒哟～");
                break;
        }
            
        message.Reply(chain);

        return MarisaPluginTaskState.CompletedTask;
    }
}
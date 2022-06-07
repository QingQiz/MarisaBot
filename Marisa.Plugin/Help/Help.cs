using System.Drawing;
using Marisa.Plugin.Shared.Help;

namespace Marisa.Plugin.Help;

[MarisaPluginDoc("给出该文档，无参数")]
[MarisaPluginCommand(true, "help", "帮助")]
public partial class Help : MarisaPluginBase
{
    private Bitmap? _bitmap;

    [MarisaPluginCommand]
    private MarisaPluginTaskState Handler(Message message, IEnumerable<MarisaPluginBase> plugins)
    {
        if (_bitmap == null)
        {
            var helpDocs = GetHelp(plugins);

            var bm    = HelpDoc.DrawHelpList(helpDocs)!;
            var bmRes = new Bitmap(bm.Width + 30, bm.Height + 30);
            var g     = Graphics.FromImage(bmRes);

            g.Clear(Color.White);
            g.DrawImage(bm, 15, 15);

            _bitmap = bmRes;
        }

        message.Reply(MessageDataImage.FromBase64(_bitmap.ToB64()));

        return MarisaPluginTaskState.CompletedTask;
    }
}
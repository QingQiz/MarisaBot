using Marisa.Plugin.Shared.Help;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Marisa.Plugin.Help;

[MarisaPluginDoc("给出该文档，无参数")]
[MarisaPluginCommand(true, "help", "帮助")]
public partial class Help : MarisaPluginBase
{
    private Image<Rgba32>? _image;

    [MarisaPluginCommand]
    private MarisaPluginTaskState Handler(Message message, IEnumerable<MarisaPluginBase> plugins)
    {
        if (_image == null)
        {
            var helpDocs = GetHelp(plugins);

            var bm    = HelpDoc.DrawHelpList(helpDocs)!;
            var bmRes = new Image<Rgba32>(bm.Width + 30, bm.Height + 30);

            bmRes.Mutate(i => i.Fill(Color.White).DrawImage(bm, 15, 15));

            _image = bmRes;
        }

        message.Reply(MessageDataImage.FromBase64(_image.ToB64()));

        return MarisaPluginTaskState.CompletedTask;
    }
}
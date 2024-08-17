using Marisa.Plugin.Shared.Help;
using Marisa.Plugin.Shared.Util;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Marisa.Plugin;

[MarisaPluginDoc("给出该文档，无参数")]
[MarisaPluginCommand(true, "help", "帮助")]
public class Help : MarisaPluginBase
{
    private Image? _image;

    [MarisaPluginCommand]
    private MarisaPluginTaskState Handler(Message message, IEnumerable<MarisaPluginBase> plugins)
    {
        if (_image == null)
        {
            const int gap = 15;

            var helpDocs = HelpGenerator.GetHelp(plugins);

            var ims = helpDocs.Select(d => d.GetImage()).ToList();

            var bmRes = new Image<Rgba32>(
                ims.Max(i => i.Width) + gap * 2,
                ims.Sum(i => i.Height) + gap * 2 + 2 * gap * (ims.Count - 1)
            ).Clear(Color.White);

            var y = gap;
            foreach (var im in ims)
            {
                bmRes.DrawImage(im, gap, y);
                y += im.Height + gap - 1;
                var y1 = y;
                bmRes.Mutate(i => i.DrawLine(Color.Pink, 2, new PointF(gap, y1), new PointF(bmRes.Width - gap, y1)));
                y += gap + 1;
            }

            _image = bmRes;
        }

        message.Reply(
            new MessageDataText("本bot为开源bot\n仙人指路：QingQiz/MarisaBot"),
            MessageDataImage.FromBase64(_image.ToB64())
        );

        return MarisaPluginTaskState.CompletedTask;
    }
}
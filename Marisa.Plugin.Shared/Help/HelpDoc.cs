using Marisa.Utils;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Marisa.Plugin.Shared.Help;

public class HelpDoc
{
    public readonly string Help;
    public readonly List<string> Commands;
    public List<HelpDoc> SubHelp = new();

    public HelpDoc(string help, List<string> commands)
    {
        Help     = help;
        Commands = commands.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
    }

    public Image GetImage(int depth = 1)
    {
        var font1 = new Font(SystemFonts.Get("Microsoft YaHei"), 22, FontStyle.Regular);

        const int subCmdMarginX = 30;
        const int subCmdMarginY = 15;

        var sd = new StringDrawer(5);

        for (var i = 0; i < Commands.Count - 1; i++)
        {
            sd.Add(Commands[i], font1, Color.DeepPink);
            sd.Add("、", font1, Color.Black);
        }

        if (Commands.Any())
        {
            sd.Add(Commands.Last(), font1, Color.DeepPink);
            sd.Add(font1, Color.Black, "：");
        }

        sd.Add(Help, font1, Color.Black);

        var subHelp = DrawHelpList(SubHelp);

        var measure = sd.Measure();

        var bmW = Math.Max((int)measure.Width, (subHelp?.Width ?? -subCmdMarginX) + subCmdMarginX);
        var bmH = (int)measure.Height + (subHelp?.Height ?? -subCmdMarginY) + subCmdMarginY;

        var bm = new Image<Rgba32>(bmW, bmH);

        bm.Clear(Color.White);

        sd.Draw(bm);
        
        if (subHelp != null)
        {
            bm.DrawImage(subHelp, subCmdMarginX, (int)(measure.Height + subCmdMarginY));
        }

        return bm;
    }

    public static Image? DrawHelpList(IEnumerable<HelpDoc> docs)
    {
        const int padding = 15;

        var ims = docs.Select(d => d.GetImage()).ToList();

        if (!ims.Any())
        {
            return null;
        }

        var bm = new Image<Rgba32>(ims.Max(im => im.Width),
            ims.Sum(im => im.Height) + padding * (ims.Count - 1));

        bm.Clear(Color.White);

        var y = 0;
        foreach (var im in ims.Take(ims.Count - 1))
        {
            bm.DrawImage(im, 0, y);
            y += im.Height + padding;
        }

        bm.DrawImage(ims.Last(), 0, y);

        return bm;
    }
}
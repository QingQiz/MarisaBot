using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Marisa.Utils;

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

    public Bitmap GetImage(int depth = 1)
    {
        var font1 = new Font("Microsoft YaHei", 22, FontStyle.Regular, GraphicsUnit.Pixel);

        const int subCmdMarginX = 30;
        const int subCmdMarginY = 15;

        var sd = new StringDrawer(5);

        for (var i = 0; i < Commands.Count - 1; i++)
        {
            sd.Add(Commands[i], font1, Brushes.DeepPink);
            sd.Add("、", font1, Brushes.Black);
        }

        if (Commands.Any())
        {
            sd.Add(Commands.Last(), font1, Brushes.DeepPink);
            sd.Add(font1, Brushes.Black, "：");
        }

        sd.Add(Help, font1, Brushes.Black);

        var subHelp = DrawHelpList(SubHelp);

        var measure = sd.Measure();

        var bmW = Math.Max((int)measure.Width, (subHelp?.Width ?? -subCmdMarginX) + subCmdMarginX);
        var bmH = (int)measure.Height + (subHelp?.Height ?? -subCmdMarginY) + subCmdMarginY;

        var bm = new Bitmap(bmW, bmH);
        var g  = Graphics.FromImage(bm);

        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.SmoothingMode     = SmoothingMode.HighQuality;

        g.Clear(Color.White);

        sd.Draw(g);
        
        if (subHelp != null)
        {
            g.DrawImage(subHelp, subCmdMarginX, measure.Height + subCmdMarginY);
        }

        return bm;
    }

    public static Bitmap? DrawHelpList(IEnumerable<HelpDoc> docs)
    {
        const int padding = 15;

        var ims = docs.Select(d => d.GetImage()).ToList();

        if (!ims.Any())
        {
            return null;
        }

        var bm = new Bitmap(ims.Max(im => im.Width),
            ims.Sum(im => im.Height) + padding * (ims.Count - 1));

        var g = Graphics.FromImage(bm);
        g.SmoothingMode = SmoothingMode.HighQuality;

        g.Clear(Color.White);

        var y = 0;
        foreach (var im in ims.Take(ims.Count - 1))
        {
            g.DrawImage(im, 0, y);
            y += im.Height + padding;
        }

        g.DrawImage(ims.Last(), 0, y);

        return bm;
    }
}
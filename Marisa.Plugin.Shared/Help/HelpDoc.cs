using Marisa.Plugin.Shared.Util;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Marisa.Plugin.Shared.Help;

public class HelpDoc
{
    private readonly List<ReadOnlyMemory<char>> _commands;
    private readonly string _help;
    public List<HelpDoc> SubHelp = [];

    public HelpDoc(string help, List<ReadOnlyMemory<char>> commands)
    {
        _help     = help;
        _commands = commands.Where(c => !c.IsWhiteSpace()).ToList();
    }

    public Image GetImage(int depth = 1)
    {
        var font1 = new Font(SystemFonts.Get("Microsoft YaHei"), 22, FontStyle.Regular);

        const int subCmdMarginX = 30;
        const int subCmdMarginY = 15;

        var sd = new StringDrawer(5);

        for (var i = 0; i < _commands.Count - 1; i++)
        {
            sd.Add(_commands[i].ToString(), font1, Color.DeepPink);
            sd.Add("、", font1, Color.Black);
        }

        if (_commands.Count != 0)
        {
            sd.Add(_commands.Last().ToString(), font1, Color.DeepPink);
            sd.Add(font1, Color.Black, "：");
        }

        sd.Add(_help, font1, Color.Black);

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

    private static Image<Rgba32>? DrawHelpList(IEnumerable<HelpDoc> docs)
    {
        const int padding = 15;

        var ims = docs.Select(d => d.GetImage()).ToList();

        if (ims.Count == 0)
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
using System.ComponentModel;
using Marisa.Utils;
using SharpFNT;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Marisa.Plugin.Shared.Osu.Drawer;

public static class OsuFont
{
    private static readonly string OsuFontPath = Path.Join(OsuDrawerCommon.ResourcePath, "osuFont.bin");

    private static BitmapFont? _font;
    private static readonly Dictionary<int, Image> FontImage = new();

    public static Image GetCharacter(int c)
    {
        _font ??= BitmapFont.FromFile(OsuFontPath);

        var @char = _font.Characters[c];
        var page  = _font.Characters[c].Page;

        if (!FontImage.ContainsKey(page))
        {
            var pngPath = Path.Join(Path.GetDirectoryName(OsuFontPath), _font.Pages[page]);

            FontImage[page] = Image.Load(pngPath);
        }

        return new Image<Rgba32>(@char.Width, @char.Height).DrawImageCenter(
            FontImage[page].Clone(im => im.Crop(new Rectangle(@char.X, @char.Y, @char.Width, @char.Height)))
        );
    }

    public static (int, int) GetModeCharacter(string mode)
    {
        return mode.ToLower() switch
        {
            // convert
            "1k"  => (0, 0),
            "2k"  => (0, 0),
            "3k"  => (0, 0),
            "4k"  => (57355, 0),
            "5k"  => (57356, 0),
            "6k"  => (57357, 0),
            "7k"  => (57358, 0),
            "8k"  => (57359, 0),
            "9k"  => (0, 0),
            "10k" => (0, 0),
            // easier
            "ez" => (57406, 1),
            "ht" => (57408, 1),
            "nf" => (57412, 1),
            // harder
            "dt" => (57405, 2),
            "fl" => (57407, 2),
            "hr" => (57409, 2),
            "fi" => (57410, 2),
            "hd" => (57410, 2),
            "nc" => (57411, 2),
            "sd" => (57415, 2),
            "pf" => (57417, 2),
            // auto
            "rd" => (57361, 3),
            "at" => (57403, 3),
            "ap" => (57402, 3),
            "cn" => (57404, 3),
            "rx" => (57413, 3),
            "so" => (57414, 3),
            _    => (0, 0)
        };
    }

    public static int BorderChar => 57418;

    private static void Export(string exportPath)
    {
        var font = BitmapFont.FromFile(OsuFontPath);

        Directory.CreateDirectory(exportPath);
        foreach (var i in font.Characters)
        {
            var c = i.Value;

            var pngName = Path.Join(Path.GetDirectoryName(OsuFontPath), font.Pages[c.Page]);
            var image   = Image.Load(pngName);

            image.Mutate(im => im.Crop(new Rectangle(c.X, c.Y, c.Width, c.Height)));
            image.SaveAsPng(Path.Join(exportPath, $"{i.Key}.png"));
        }
    }

    public static (Color, Color) GetColorByModeType(int t)
    {
        return t switch
        {
            0 => (Color.ParseHex("#8f68f3"), Color.ParseHex("#2d1e58")),
            1 => (Color.ParseHex("#c2fc6f"), Color.ParseHex("#3c581e")),
            2 => (Color.ParseHex("#fc6f6f"), Color.ParseHex("#581e1e")),
            3 => (Color.ParseHex("#6fdefc"), Color.ParseHex("#1e4558")),
            _ => throw new InvalidEnumArgumentException()
        };
    }
}
using Marisa.Plugin.Shared.Util;
using Marisa.Plugin.Shared.Util.Cacheable;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Marisa.Plugin.Shared.Osu.Drawer;

public static class OsuModDrawer
{
    public static Image GetModIcon(string mod)
    {
        mod = mod.ToUpper();

        return new CacheableImage(Path.Join(OsuDrawerCommon.TempPath, $"mod-{mod}.png"), () =>
        {
            var (iconId, type) = OsuFont.GetModeCharacter(mod);
            var color = OsuFont.GetColorByModeType(type);

            var border = OsuFont.GetCharacter(OsuFont.BorderChar);
            border.Mutate(i =>
            {
                i.SetGraphicsOptions(new GraphicsOptions
                {
                    AlphaCompositionMode = PixelAlphaCompositionMode.SrcIn
                });
                i.Fill(color.Item1);
            });

            if (iconId == 0)
            {
                var f = OsuDrawerCommon.FontExo2.CreateFont(40);
                border.DrawTextCenter(mod, f, color.Item2, withSpace: false);
            }
            else
            {
                var icon = OsuFont.GetCharacter(iconId).ResizeY(26);
                icon.Mutate(i =>
                {
                    i.SetGraphicsOptions(new GraphicsOptions
                    {
                        AlphaCompositionMode = PixelAlphaCompositionMode.SrcIn
                    });
                    i.Fill(color.Item2);
                });

                border.DrawImageCenter(icon, offsetY: -10);

                var font = OsuDrawerCommon.FontExo2.CreateFont(15);

                var m = mod.Measure(font);

                var imgText = new Image<Rgba32>((int)m.Width + 15, (int)m.Height + 10).Clear(color.Item2);
                imgText.DrawTextCenter(mod.ToUpper(), font, color.Item1, withSpace: false);

                border.DrawImageCenter(imgText.RoundCorners(imgText.Height / 2 + 1), offsetY: 17);
            }

            return border;
        }).Value;
    }

    public static Image GetModIconWithoutText(string mod)
    {
        mod = mod.ToUpper();

        return new CacheableImage(Path.Join(OsuDrawerCommon.TempPath, $"mod-{mod}-NoText.png"), () =>
        {
            var (iconId, type) = OsuFont.GetModeCharacter(mod);
            var color = OsuFont.GetColorByModeType(type);

            var border = OsuFont.GetCharacter(OsuFont.BorderChar);
            border.Mutate(i =>
            {
                i.SetGraphicsOptions(new GraphicsOptions
                {
                    AlphaCompositionMode = PixelAlphaCompositionMode.SrcIn
                });
                i.Fill(color.Item1);
            });

            if (iconId == 0)
            {
                var f = OsuDrawerCommon.FontExo2.CreateFont(40);
                border.DrawTextCenter(mod, f, color.Item2, withSpace: false);
            }
            else
            {
                var icon = OsuFont.GetCharacter(iconId).ResizeY(border.Height - 10);
                icon.Mutate(i =>
                {
                    i.SetGraphicsOptions(new GraphicsOptions
                    {
                        AlphaCompositionMode = PixelAlphaCompositionMode.SrcIn
                    });
                    i.Fill(color.Item2);
                });

                border.DrawImageCenter(icon);
            }

            return border;
        }).Value;
    }
}
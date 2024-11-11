using Flurl.Http;
using Marisa.Plugin.Shared.Configuration;
using Marisa.Plugin.Shared.Util;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Marisa.Plugin.Shared.Osu.Drawer;

public static class OsuDrawerCommon
{
    public static string TempPath => ConfigurationManager.Configuration.Osu.TempPath;
    public static string ResourcePath => ConfigurationManager.Configuration.Osu.ResourcePath;

    public static FontFamily FontIcon => SystemFonts.Get("Segoe UI Symbol");
    public static FontFamily FontExo2 => SystemFonts.Get("Exo 2");
    public static FontFamily FontYaHei => SystemFonts.Get("Microsoft YaHei");

    public static async Task<Image> GetCacheOrDownload(Uri uri, string ext = "")
    {
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = uri.LocalPath.Split('.').Last();
            ext = ext.Equals(uri.LocalPath) ? uri.Query.Split('.').Last() : ext;
        }

        var filename = uri.ToString().GetSha256Hash() + '.' + ext;

        return await GetCacheOrDownload(filename, uri);
    }

    private static async Task<Image> GetCacheOrDownload(string filename, Uri uri)
    {
        var filepath = Path.Join(TempPath, filename);
        if (File.Exists(filepath))
        {
            return (await Image.LoadAsync(filepath)).CloneAs<Rgba32>();
        }

        var bytes = await uri.GetBytesAsync();
        await File.WriteAllBytesAsync(filepath, bytes);

        return (await Image.LoadAsync(filepath)).CloneAs<Rgba32>();
    }

    public static Image GetIcon(string iconName)
    {
        return Image.Load(Path.Join(ResourcePath, $"icon-{iconName}.png")).CloneAs<Rgba32>();
    }

    public static async Task<Image> GetAvatar(Uri avatarUri)
    {
        return await GetCacheOrDownload(avatarUri);
    }

    public static Image GetRankIcon(string rankName)
    {
        return rankName.ToLower() switch
        {
            "d"   => GetIcon("rank-d"),
            "c"   => GetIcon("rank-c"),
            "b"   => GetIcon("rank-b"),
            "a"   => GetIcon("rank-a"),
            "s"   => GetIcon("rank-s"),
            "sh"  => GetIcon("rank-s-s"),
            "ss"  => GetIcon("rank-ss"),
            "ssh" => GetIcon("rank-ss-s"),
            "x"   => GetIcon("rank-ss"),
            "xh"  => GetIcon("rank-ss-s"),
            _     => throw new ArgumentOutOfRangeException()
        };
    }
}
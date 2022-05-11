using System.Drawing;
using Marisa.Plugin.Shared.Configuration;
using Marisa.Utils;

namespace Marisa.Plugin.Shared.MaiMaiDx
{
    public class ResourceManager
    {
        public static readonly string ResourcePath = ConfigurationManager.Configuration.MaiMai.ResourcePath;
        public static readonly string TempPath = ConfigurationManager.Configuration.MaiMai.TempPath;

        public static Bitmap GetImage(string imgName, int width = 0, int height = 0)
        {
            var imgPath = ResourcePath + "/pic";
            var img     = (Bitmap)Image.FromFile($"{imgPath}/{imgName}");

            if (width != 0 && height != 0)
            {
                img = img.Resize(width, height);
            }

            return img;
        }

        public static Bitmap GetCover(long songId, bool resize = true)
        {
            var coverPath = ResourcePath + "/cover";

            var cp = $"{coverPath}/{songId}.png";

            if (!File.Exists(cp))
            {
                cp = cp[..^3] + "jpg";
            }

            if (!File.Exists(cp))
            {
                return GetCover(1000, resize);
            }

            var img = (Bitmap)Image.FromFile(cp);
            return resize ? img.Resize(200, 200) : img;
        }

        public static (Bitmap, Color) GetCoverBackground(long songId)
        {
            return GetCover(songId).GetCoverBackground();
        }
    }
}
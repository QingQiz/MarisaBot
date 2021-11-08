using System.Collections.Generic;
using System.Drawing;
using System.IO;
using QQBOT.Core.Util;

namespace QQBOT.Core.Plugin.PluginEntity.MaiMaiDx
{
    public class ResourceManager
    {
        public const string ResourcePath = "Plugin/PluginResource/MaiMaiDx";

        private static readonly Dictionary<string, Bitmap> ImgCache = new();
        private static readonly Dictionary<long, Bitmap> CoverCache = new();
        private static readonly Dictionary<long, (Bitmap, Color)> CoverBackgroundCache = new();

        public static Bitmap GetImage(string imgName, int width = 0, int height = 0)
        {
            const string imgPath = ResourcePath + "/pic";

            if (!ImgCache.ContainsKey(imgName))
            {
                ImgCache[imgName] = (Bitmap)Image.FromFile($"{imgPath}/{imgName}");

                if (width != 0 && height != 0)
                {
                    ImgCache[imgName] = ImgCache[imgName].Resize(width, height);
                }

            }

            var ret = ImgCache[imgName].Copy();

            if (width != 0 && height != 0)
                if (ret.Width != width || ret.Height != height)
                {
                    ret = ret.Resize(width, height);
                }

            ImgCache[imgName] = ret;
            return ret.Copy();
        }

        public static Bitmap GetCover(long songId)
        {
            const string coverPath = ResourcePath + "/cover";

            if (!CoverCache.ContainsKey(songId))
            {
                var cp = $"{coverPath}/{songId}.png";

                if (!File.Exists(cp))
                {
                    cp = cp[..^3] + "jpg";
                }

                if (!File.Exists(cp))
                {
                    CoverCache[songId] = GetCover(1000);
                }
                else
                {
                    CoverCache[songId] = Image.FromFile(cp).Resize(200, 200);
                }
            }

            return CoverCache[songId].Copy();
        }

        public static (Bitmap, Color) GetCoverBackground(long songId)
        {
            if (!CoverBackgroundCache.ContainsKey(songId))
                CoverBackgroundCache[songId] = GetCover(songId).GetCoverBackground();
            return (CoverBackgroundCache[songId].Item1.Copy(), CoverBackgroundCache[songId].Item2);
        }
    }
}
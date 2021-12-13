using System.Collections.Generic;
using System.Configuration;
using System.Drawing;

namespace QQBOT.Core.Plugin.PluginEntity.Arcaea
{
    public static class ResourceManager
    {
        public static readonly string ResourcePath = ConfigurationManager.AppSettings["Arcaea.ResourcePath"];
        public static readonly string TempPath = ConfigurationManager.AppSettings["Arcaea.TempPath"];

        private static readonly Dictionary<string, Bitmap> CoverCache = new();

        public static Bitmap GetCover(string coverName)
        {
            var coverPath = ResourcePath + "/cover";

            if (!CoverCache.ContainsKey(coverName))
            {
                var cp = $"{coverPath}/{coverName}";

                CoverCache[coverName] = (Bitmap)Image.FromFile(cp);
            }

            return CoverCache[coverName];
        }
    }
}
using System.Drawing;
using Marisa.Plugin.Shared.Configuration;

namespace Marisa.Plugin.Shared.Arcaea
{
    public static class ResourceManager
    {
        public static readonly string ResourcePath = ConfigurationManager.Configuration.Arcaea.ResourcePath;
        public static readonly string TempPath = ConfigurationManager.Configuration.Arcaea.TempPath;

        public static Bitmap GetCover(string coverName)
        {
            var coverPath = ResourcePath + "/cover";
            return (Bitmap)Image.FromFile($"{coverPath}/{coverName}");
        }
    }
}
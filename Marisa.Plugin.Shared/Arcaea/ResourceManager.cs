using Marisa.Plugin.Shared.Configuration;
using SixLabors.ImageSharp;

namespace Marisa.Plugin.Shared.Arcaea
{
    public static class ResourceManager
    {
        public static readonly string ResourcePath = ConfigurationManager.Configuration.Arcaea.ResourcePath;
        public static readonly string TempPath = ConfigurationManager.Configuration.Arcaea.TempPath;

        public static Image GetCover(string coverName)
        {
            var coverPath = ResourcePath + "/cover";
            return Image.Load($"{coverPath}/{coverName}");
        }
    }
}
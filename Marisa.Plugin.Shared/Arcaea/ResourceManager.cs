using System.Configuration;
using System.Drawing;

namespace Marisa.Plugin.Shared.Arcaea
{
    public static class ResourceManager
    {
        public static readonly string ResourcePath = ConfigurationManager.AppSettings["Arcaea.ResourcePath"]!;
        public static readonly string TempPath = ConfigurationManager.AppSettings["Arcaea.TempPath"]!;

        public static Bitmap GetCover(string coverName)
        {
            var coverPath = ResourcePath + "/cover";
            return (Bitmap)Image.FromFile($"{coverPath}/{coverName}");
        }
    }
}
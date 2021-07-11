using System.Threading.Tasks;
using QQBOT.Core.Utils;

namespace QQBOT.Core
{
    public static class Program
    {
        private static async Task Main(string[] args)
        {
            await StartUp.Start();
        }
    }
}
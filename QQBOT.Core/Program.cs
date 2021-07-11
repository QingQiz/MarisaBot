using System;
using System.Configuration;
using System.Threading.Tasks;
using QQBOT.Core.Utils;

namespace QQBOT.Core
{
    public static class Program
    {
        private static async Task Main(string[] args)
        {
            var x = ConfigurationManager.AppSettings;
            foreach (var xAllKey in x.AllKeys)
            {
                Console.WriteLine(xAllKey);
            }
            return;
            await StartUp.Start();
        }
    }
}
using Microsoft.Extensions.DependencyInjection;
using QQBot.MiraiHttp;

namespace QQBot.StartUp
{
    public static class Program
    {
        private static async Task Main(string[] args)
        {
            var provider = new Configuration(args).Config();

            var session = provider.GetService<MiraiHttpSession>()!;

            Console.WriteLine("---------------------------------------------------------------");

            while (true)
            {
                try
                {
                    Console.WriteLine("-- Init...");
                    await session.Init();
                    Console.WriteLine("-- Running ");
                    await session.Run();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
            // ReSharper disable once FunctionNeverReturns
        }
    }
}
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
            Console.WriteLine("-- Running ");
            // await session.Run();
            await session.Invoke();
        }
    }
}
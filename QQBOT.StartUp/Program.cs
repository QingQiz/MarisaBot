using QQBot.MiraiHttp;
using QQBot.MiraiHttp.Plugin;
using QQBot.Plugin;

namespace QQBot.StartUp
{
    public static class Program
    {
        private static async Task Main(string[] args)
        {
            var session = new MiraiHttpSession(args[0], long.Parse(args[1]), args[2]);

            // add plugins to session
            var plugins = PluginUtils.EnabledPlugins();

            // Log plugin info
            Console.WriteLine("---------------------------------------------------------------");
            Console.WriteLine("-- Adding Plugins");
            foreach (var plugin in plugins)
            {
                Console.WriteLine($"Enabled plugin: `{plugin}`");
                session.AddPlugin((MiraiPluginBase)Activator.CreateInstance(plugin)!);
            }

            Console.WriteLine("---------------------------------------------------------------");

            while (true)
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
            // ReSharper disable once FunctionNeverReturns
        }
    }
}
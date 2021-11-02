using System;
using System.Linq;
using System.Threading.Tasks;
using QQBOT.Core.Attribute;
using QQBOT.Core.MiraiHttp;
using QQBOT.Core.Plugin;

namespace QQBOT.Core
{
    public static class Program
    {
        private static async Task Main(string[] args)
        {
            var session = new MiraiHttpSession(args[0], args[1], args[2]);

            // add plugins to session
            var plugins = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetTypes()
                    .Where(t => t.GetCustomAttributes(typeof(MiraiPluginAttribute), true) is {Length: > 0})
                    .Where(t => t.GetCustomAttributes(typeof(MiraiPluginDisabledAttribute), false) is not
                        {Length: > 0}))
                .SelectMany(t => t);
            
            // Log plugin info
            Console.WriteLine("---------------------------------------------------------------");
            Console.WriteLine("-- Adding Plugins");
            foreach (var plugin in plugins)
            {
                Console.WriteLine($"Enabled plugin: `{plugin}`");
                session.AddPlugin((PluginBase)Activator.CreateInstance(plugin));
            }
            Console.WriteLine("---------------------------------------------------------------");
            Console.WriteLine("-- Running ");

            await session.Run();
        }
    }
}
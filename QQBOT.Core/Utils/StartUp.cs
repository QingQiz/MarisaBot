using System;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using Mirai_CSharp;
using Mirai_CSharp.Models;
using Mirai_CSharp.Plugin;
using QQBOT.Core.Attribute;
using QQBOT.Core.Plugin.Core;
using QQBOT.EntityFrameworkCore;

namespace QQBOT.Core.Utils
{
    public static class StartUp
    {
        public static async Task Start()
        {
            ConfigAuditLog();
            await ConfigAndRunMirai();
        }

        private static void ConfigAuditLog()
        {
            Audit.Core.Configuration.Setup()
                .UseCustomProvider(new BotAuditDataProvider());
        }

        private static async Task ConfigAndRunMirai()
        {
            var host = ConfigurationManager.AppSettings["MiraiHost"] ??
                       throw new InvalidOperationException("必须在 `App.config` 中填写 `MiraiHost`");
            var port = int.Parse(ConfigurationManager.AppSettings["MiraiPort"] ??
                                 throw new InvalidOperationException("必须在 `App.config` 中填写 `MiraiPort`"));
            var authKey = ConfigurationManager.AppSettings["MiraiAuthKey"] ??
                          throw new InvalidOperationException("必须在 `App.config` 中填写 `MiraiAuthKey`");
            var options = new MiraiHttpSessionOptions(host, port, authKey);

            // create session
            await using var session = new MiraiHttpSession();

            // add plugins to session
            var plugins = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetTypes()
                    .Where(t => t.GetCustomAttributes(typeof(MiraiPluginAttribute), true) is {Length: > 0})
                    .Where(t => t.GetCustomAttributes(typeof(MiraiPluginDisabledAttribute), false) is not
                        {Length: > 0}))
                .SelectMany(t => t);

            // Log plugin info
            Console.WriteLine("---------------------------------------------------------------");
            Console.WriteLine("-- Add Plugins");
            foreach (var plugin in plugins)
            {
                Console.WriteLine($"Enabled plugin: `{plugin}`");
                session.AddPlugin(((IPlugin) Activator.CreateInstance(plugin))!);
            }
            // plugin fallback
            session.AddPlugin(((IPlugin) Activator.CreateInstance(typeof(UnHandledMessage)))!);

            Console.WriteLine("---------------------------------------------------------------");

            // run
            var qq = long.Parse(ConfigurationManager.AppSettings["QQNumber"] ??
                                 throw new InvalidOperationException("必须在 `App.config` 中填写 `QQNumber`"));
            await session.ConnectAsync(options, qq);

            // waiting for keyboard
            Console.WriteLine("-- Running");
            while (true)
            {
                if (await Console.In.ReadLineAsync() != "e") continue;

                Console.WriteLine("---------------------------------------------------------------");
                Console.WriteLine("exit..");
                break;
            }
        }
    }
}
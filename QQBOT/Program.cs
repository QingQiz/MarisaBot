using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mirai_CSharp;
using Mirai_CSharp.Models;
using Mirai_CSharp.Plugin;
using QQBOT.plugin;

namespace QQBOT
{
    public static class Program
    {
        static async Task Main(string[] args)
        {
            // ReSharper disable StringLiteralTypo
            var options = new MiraiHttpSessionOptions("127.0.0.1", 18080, "SCHW_KEY_SCHWBOT");
            // ReSharper restore StringLiteralTypo
            
            // create session
            await using var session = new MiraiHttpSession();

            await session.ConnectAsync(options, 2096937554);

            while (true)
            {
                if (await Console.In.ReadLineAsync() == "e") return;
            }
        }
    }
}
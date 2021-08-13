using System;
using System.Collections.Generic;
using System.Linq;

namespace QQBOT.Core.Attribute
{
    public class MiraiPluginAttribute : System.Attribute
    {
        public readonly string CommandPrefix;
        // private static readonly List<string> PrefixList = new();

        public MiraiPluginAttribute(string commandPrefix = null)
        {
            // if (commandPrefix != null)
            // {
            //     if (PrefixList.Any(p => p == commandPrefix))
            //     {
            //         throw new ArgumentException($"Command prefix `{commandPrefix}` has already been used.");
            //     }
            //     PrefixList.Add(commandPrefix);
            // }

            CommandPrefix = commandPrefix;
        }
    }

    public class MiraiPluginDisabledAttribute : System.Attribute
    {
        
    }
}
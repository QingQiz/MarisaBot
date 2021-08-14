using System.Collections.Generic;
using Mirai_CSharp.Models;

namespace QQBOT.Core.Utils
{
    public static class MessageChain
    {
        public static string GetMessage(this IMessageBase[] message)
        {
            return string.Join('\n', message[1..] as IEnumerable<IMessageBase>);
        }

        public static string GetMessageId(this IMessageBase[] message)
        {
            return message[0].ToString();
        }

        public static bool BeginWith(this IMessageBase[] message, string prefix)
        {
            var m = message.GetMessage().ToLower();
            var p = prefix.ToLower().ToLower();

            if (m == p) return true;
            if (p == "") return true;

            if (m.StartsWith(p + ' ')) return true;
            // ReSharper disable once ConvertIfStatementToReturnStatement
            if (m.StartsWith(p + '\t')) return true;

            return false;
        }

        public static string GetArguments(this IMessageBase[] message, string prefix)
        {
            return !message.BeginWith(prefix) ? null : message.GetMessage()[prefix.Length..].Trim();
        }
    }
}
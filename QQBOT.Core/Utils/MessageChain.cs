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
            return message.GetMessage().ToLower().StartsWith(prefix.ToLower());
        }
    }
}
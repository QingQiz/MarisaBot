using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using QQBOT.Core.MiraiHttp;
using QQBOT.Core.MiraiHttp.Entity;
using QQBOT.Core.Util;

namespace QQBOT.Core.Plugin
{
    public class Select : PluginBase
    {
        private (string a, string b)? Parser(string msg)
        {
            msg = msg.TrimEnd('?').TrimEnd('？').TrimEnd('呢');

            var verb  = new[] {"吃", "买", "穿", "喝", "要", "是", "去", "选", "回", "写", "看", "打", "导", "拉", "开" };

            var prefix = msg.CheckPrefix(verb).ToList();

            if (prefix.Count == 0) return null;

            var (p, _) = prefix.First();

            var regex = new Regex($"{p}(.*?)还是(.*)");

            var match = regex.Match(msg);

            if (match.Groups.Count != 3)
            {
                return null;
            }

            var a = p + match.Groups[1].Value;
            var b = match.Groups[2].Value;

            if (b[0] == '不')
            {
                return (a, b);
            }
            return b.TrimStart(verb) == null ? (a, p + b) : (a, b);
        }

        public override async Task FriendMessageHandler(MiraiHttpSession session, Message message)
        {
            var msg = message.MessageChain!.PlainText;

            var res = Parser(msg);

            if (res == null) return;

            var send = new Random().Next(0, 2) == 0 ? res.Value.a : res.Value.b;

            await session.SendFriendMessage(new Message(MessageChain.FromPlainText("建议" + send)), message.Sender!.Id);
        }

        public override async Task GroupMessageHandler(MiraiHttpSession session, Message message)
        {
            var msg = message.MessageChain!.PlainText;

            var res = Parser(msg);

            if (res == null) return;

            var send = new Random().Next(0, 2) == 0 ? res.Value.a : res.Value.b;

            await session.SendGroupMessage(new Message(MessageChain.FromPlainText("建议" + send)),
                message.GroupInfo!.Id, message.Source.Id);
        }
    }
}
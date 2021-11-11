using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using QQBOT.Core.MiraiHttp;
using QQBOT.Core.MiraiHttp.Entity;

namespace QQBOT.Core.Plugin
{
    public class Dirty : PluginBase
    {
        private readonly List<string> _dirtyWords;
        private const string ResourcePath = "Plugin/PluginResource/Dirty";

        public Dirty()
        {
            _dirtyWords = File.ReadAllText(ResourcePath + "/dirty.txt")
                .Trim()
                .Replace("\r\n", "\n")
                .Split('\n')
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
        }
        
        public override async Task EventHandler(MiraiHttpSession session, dynamic data)
        {
            switch (data.type)
            {
                case "NudgeEvent":
                    if (data.target == session.Id)
                    {
                        var word = _dirtyWords[new Random().Next(_dirtyWords.Count)];
                        if (data.subject.kind == "Group")
                        {
                            await session.SendGroupMessage(new Message(new MessageChain(new[]
                            {
                                (MessageData)new PlainMessage(word),
                            })), data.subject.id);
                        }
                        else
                        {
                            await session.SendFriendMessage(
                                new Message(MessageChain.FromPlainText(word)), data.subject.id);
                        }
                    }
                    break;
            }
        }
    }
}
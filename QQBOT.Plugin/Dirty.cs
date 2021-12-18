using System.Configuration;
using QQBot.MiraiHttp;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Entity.MessageData;
using QQBot.MiraiHttp.Plugin;

namespace QQBot.Plugin
{
    public class Dirty : MiraiPluginBase
    {
        private readonly List<string> _dirtyWords;
        private static readonly string ResourcePath = ConfigurationManager.AppSettings["Dirty.ResourcePath"]!;

        public Dirty()
        {
            _dirtyWords = File.ReadAllText(ResourcePath + "/dirty.txt")
                .Trim()
                .Replace("\r\n", "\n")
                .Split('\n')
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
        }

        protected override async Task<MiraiPluginTaskState> EventHandler(MiraiHttpSession session, dynamic data)
        {
            switch (data.type)
            {
                case "NudgeEvent":
                    if (data.target == session.Id)
                    {
                        var word = _dirtyWords[new Random().Next(_dirtyWords.Count)];
                        if (data.fromId == 642191352) word = "别戳啦！";

                        if (data.subject.kind == "Group")
                            await session.SendGroupMessage(new Message(new MessageChain(new MessageData[]
                            {
                                new AtMessage(data.fromId),
                                new PlainMessage(" "),
                                new PlainMessage(word)
                            })), data.subject.id);
                        else
                            await session.SendFriendMessage(
                                new Message(MessageChain.FromPlainText(word)), data.subject.id);

                        return MiraiPluginTaskState.CompletedTask;
                    }

                    break;
            }

            return MiraiPluginTaskState.NoResponse;
        }
    }
}
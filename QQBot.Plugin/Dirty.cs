using System.Configuration;
using QQBot.MiraiHttp;
using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Entity.MessageData;
using QQBot.MiraiHttp.Plugin;

namespace QQBot.Plugin;

public class Dirty : MiraiPluginBase
{
    private readonly List<string> _dirtyWords;
    private static readonly string ResourcePath = ConfigurationManager.AppSettings["Dirty.ResourcePath"]!;
    private readonly MessageSenderProvider _sender;

    public Dirty(MessageSenderProvider ms)
    {
        _dirtyWords = File.ReadAllText(ResourcePath + "/dirty.txt")
            .Trim()
            .Replace("\r\n", "\n")
            .Split('\n')
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
        _sender = ms;
    }

    public override Task EventHandler(MiraiHttpSession session, dynamic data)
    {
        switch (data.type)
        {
            case "NudgeEvent":
                if (data.target == session.Id)
                {
                    var word = _dirtyWords[new Random().Next(_dirtyWords.Count)];

                    if (data.fromId == 642191352) word = "别戳啦！";

                    if (data.subject.kind == "Group")
                    {
                        _sender.Send(new MessageChain(new MessageData[]
                        {
                            new AtMessage(data.fromId),
                            new PlainMessage(" "),
                            new PlainMessage(word)
                        }), MiraiMessageType.GroupMessage, data.subject.id, null);
                        
                    }
                    else
                    {
                        _sender.Send(MessageChain.FromPlainText(word), MiraiMessageType.FriendMessage, data.subject.id, null); 
                    }
                }
                break;
        }

        return Task.CompletedTask;
    }
}
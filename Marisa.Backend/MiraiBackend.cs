using System.ComponentModel;
using System.Reflection;
using System.Threading.Tasks.Dataflow;
using Flurl;
using Flurl.Http;
using Marisa.BotDriver.DI;
using Marisa.BotDriver.DI.Message;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Entity.Sender;
using Marisa.BotDriver.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace Marisa.Backend;

public class MiraiBackend : BotDriver.BotDriver
{
    private readonly string _serverAddress;
    private string _session = null!;
    private readonly string _authKey;
    public long Id { get; }

    public MiraiBackend(IServiceProvider serviceProvider, IEnumerable<MarisaPluginBase> plugins, DictionaryProvider dict, MessageSenderProvider messageSenderProvider, MessageQueueProvider messageQueueProvider) : base(serviceProvider, plugins, dict, messageSenderProvider, messageQueueProvider)
    {
        _serverAddress      = dict["ServerAddress"];
        Id                  = dict["QQ"];
        _authKey            = dict["AuthKey"];
    }

    public new static IServiceCollection Config(Assembly pluginAssembly)
    {
        var sc = BotDriver.BotDriver.Config(pluginAssembly);
        sc.AddScoped<MiraiBackend>();
        return sc;
    }

    private async Task Auth()
    {
        // get session
        var login = await (await $"{_serverAddress}/verify".PostJsonAsync(new { verifyKey = _authKey })).GetJsonAsync();
        CheckResponse(login);

        _session = login.session;

        CheckResponse(await
            (await $"{_serverAddress}/bind".PostJsonAsync(new { sessionKey = _session, qq = Id })).GetJsonAsync());
    }

    private static void CheckResponse(dynamic response)
    {
        if (response.code != 0) throw new Exception($"[Code {response.code}] {response.msg}");
    }

    protected override async Task RecvMessage()
    {
        var retry = 0;
        while (true)
        {
            try
            {
                await Auth();
                retry = 0;
                await RecvMessageInner();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());

                await Task.Delay(TimeSpan.FromSeconds(3));

                if (retry++ > 10) return;
            }
        }
    }

    private async Task RecvMessageInner()
    {
        var reportAddress = $"{_serverAddress}/countMessage";
        var request       = reportAddress.SetQueryParam("sessionKey", _session);

        while (true)
        {
            var msgCnt = await request.GetJsonAsync();
            CheckResponse(msgCnt);

            if (msgCnt.data == 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));
                continue;
            }

            var msg = await $"{_serverAddress}/fetchMessage"
                .SetQueryParams(new
                {
                    sessionKey = _session,
                    count      = msgCnt.data
                }).GetJsonAsync();
            CheckResponse(msg);

            foreach (var m in msg.data)
            {
                if (m.type.Contains("Message"))
                {
                    var message = new Message(new MessageChain(m.messageChain), MessageSenderProvider)
                    {
                        Type = m.type switch
                        {
                            "StrangerMessage" => MessageType.StrangerMessage,
                            "FriendMessage"   => MessageType.FriendMessage,
                            "GroupMessage"    => MessageType.GroupMessage,
                            "TempMessage"     => MessageType.TempMessage,
                            _                 => throw new ArgumentOutOfRangeException()
                        }
                    };

                    if (message.Type is MessageType.FriendMessage or MessageType.StrangerMessage)
                    {
                        message.Sender =
                            new SenderInfo(m.sender.id, m.sender.nickname, m.sender.remark);
                    }
                    else
                    {
                        message.Sender =
                            new SenderInfo(m.sender.id, m.sender.memberName, permission: m.sender.permission);
                        message.GroupInfo =
                            new GroupInfo(m.sender.group.id, m.sender.group.name, m.sender.group.permission);
                    }

                    MessageQueueProvider.RecvQueue.Post(message);
                }
                else // Event
                {
                    switch (m.type)
                    {
                        case "NudgeEvent":
                        {
                            var message = new Message(
                                new MessageChain(new MessageDataNudge(m.target, m.fromId, m.subject.id, m.action, m.suffix)),
                                MessageSenderProvider)
                            {
                                Type = m.subject.kind switch
                                {
                                    "Group"  => MessageType.GroupMessage,
                                    "Friend" => MessageType.FriendMessage,
                                    _        => throw new ArgumentOutOfRangeException()
                                },
                                Sender = new SenderInfo(m.fromId, null, null, null),
                            };

                            if (message.Type == MessageType.GroupMessage)
                            {
                                message.GroupInfo = new GroupInfo(m.subject.id, null, null);
                            }
                            MessageQueueProvider.RecvQueue.Post(message);
                            break;
                        }
                    }
                }
            }
        }
    }

    protected override async Task SendMessage()
    {
        var sendFriendMessageAddress = $"{_serverAddress}/sendFriendMessage";
        var sendGroupMessageAddress  = $"{_serverAddress}/sendGroupMessage";
        var sendTempMessageAddress   = $"{_serverAddress}/sendTempMessage";

        async Task SendFriendMessage(MessageChain message, long target, long? quote = null)
        {
            dynamic toSend = new
            {
                sessionKey = _session, target, quote,
                messageChain = message.Messages
                    .Select(m => m.ToObject()).ToList()
            };

            await sendFriendMessageAddress.PostJsonAsync((object)toSend);
        }

        async Task SendGroupMessage(MessageChain message, long target, long? quote = null)
        {
            dynamic toSend = new
            {
                sessionKey = _session, target, quote,
                messageChain = message.Messages
                    .Select(m => m.ToObject()).ToList()
            };

            await sendGroupMessageAddress.PostJsonAsync((object)toSend);
        }

        async Task SendTempMessage(MessageChain message, long target, long? groupId, long? quote = null)
        {
            dynamic toSend = new
            {
                sessionKey = _session,
                qq         = target,
                group      = groupId,
                quote,
                messageChain = message.Messages
                    .Select(m => m.ToObject()).ToList()
            };

            await sendTempMessageAddress.PostJsonAsync((object)toSend);
        }

        var taskList = new List<Task>();

        while (await MessageQueueProvider.SendQueue.OutputAvailableAsync())
        {
            var s = await MessageQueueProvider.SendQueue.ReceiveAsync();

            switch (s.Type)
            {
                case MessageType.GroupMessage:
                    taskList.Add(SendGroupMessage(s.MessageChain, s.ReceiverId, s.QuoteId));
                    break;
                case MessageType.FriendMessage:
                    taskList.Add(SendFriendMessage(s.MessageChain, s.ReceiverId, s.QuoteId));
                    break;
                case MessageType.TempMessage:
                    taskList.Add(SendTempMessage(s.MessageChain, s.ReceiverId, s.GroupId, s.QuoteId));
                    break;
                case MessageType.StrangerMessage:
                    throw new NotImplementedException();
                default:
                    throw new InvalidEnumArgumentException();
            }

            if (taskList.Count < 100) continue;

            await Task.WhenAll(taskList);
            taskList.Clear();
        }

        await Task.WhenAll(taskList);
    }
}
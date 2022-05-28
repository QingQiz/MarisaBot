using System.ComponentModel;
using System.Dynamic;
using System.Reflection;
using Flurl;
using log4net;
using Marisa.BotDriver.DI;
using Marisa.BotDriver.DI.Message;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Entity.MessageSender;
using Marisa.BotDriver.Plugin;
using Marisa.Utils;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Websocket.Client;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Marisa.Backend;

public class MiraiBackend : BotDriver.BotDriver
{
    private readonly WebsocketClient _wsClient;
    private readonly ILog _logger;

    public MiraiBackend(
        IServiceProvider serviceProvider, IEnumerable<MarisaPluginBase> plugins,
        DictionaryProvider dict, MessageSenderProvider messageSenderProvider, MessageQueueProvider messageQueueProvider,
        ILog logger) : base(serviceProvider, plugins, dict, messageSenderProvider, messageQueueProvider)
    {
        _logger = logger;
        string serverAddress = dict["ServerAddress"];
        long   id            = dict["QQ"];
        string authKey       = dict["AuthKey"];

        _wsClient = new WebsocketClient(new Uri(
            $"{serverAddress}/all"
                .SetQueryParam("verifyKey", authKey)
                .SetQueryParam("qq", id)))
        {
            ReconnectTimeout = TimeSpan.MaxValue,
        };

        _wsClient.ReconnectionHappened.Subscribe(_ => { _logger.Warn("Reconnection happened"); });
    }

    public new static IServiceCollection Config(Assembly pluginAssembly)
    {
        var sc = BotDriver.BotDriver.Config(pluginAssembly);
        sc.AddScoped<MiraiBackend>();
        return sc;
    }

    #region MessageCoverter

    private Message MessageToMessage(dynamic m)
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

        return message;
    }

    private Message? EventToMessage(dynamic m)
    {
        switch (m.type)
        {
            case "NudgeEvent":
            {
                var message =
                    new Message(
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

                return message;
            }
        }

        return null;
    }

    private Message? MessageConverter(ResponseMessage msgIn)
    {
        var mExpando = JsonConvert.DeserializeObject<ExpandoObject>(msgIn.Text);

        var m = (mExpando as dynamic).data;

        var mDict = (m as IDictionary<string, object>)!;

        if (mDict.ContainsKey("code"))
        {
            var code = mDict["code"].ToString();
            if (code != "0")
            {
                _logger.Warn(mDict["msg"]);
            }

            return null;
        }

        if (m.type.Contains("Message"))
        {
            if (MessageToMessage(m) is Message message)
            {
                return message;
            }
            else
            {
                _logger.Warn($"Can not convert message `{msgIn.Text}` to Message");
            }
        }
        else if (m.type.Contains("Event")) // Event
        {
            if (EventToMessage(m) is Message message)
            {
                return message;
            }
            else
            {
                _logger.Warn($"Can not convert event `{msgIn.Text}` to Message");
            }
        }
        else
        {
            _logger.Warn($"Unknown message {msgIn.Text}");
        }

        return null;
    }

    #endregion

    protected override Task RecvMessage()
    {
        async void OnMessage(ResponseMessage msg)
        {
            try
            {
                var message = MessageConverter(msg);

                if (message == null) return;
                _logger.Info(message.GroupInfo == null
                    ? $"({message.Sender!.Id,11}) -> {message.MessageChain}".Escape()
                    : $"({message.GroupInfo.Id,11}) => ({message.Sender!.Id,11}) -> {message.MessageChain}".Escape());
                await MessageQueueProvider.RecvQueue.Writer.WriteAsync(message);
            }
            catch (Exception e)
            {
                _logger.Error(e.ToString());
                _logger.Error($"Unknown data: {msg.Text}");
            }
        }

        _wsClient.MessageReceived.Subscribe(OnMessage);
        return Task.CompletedTask;
    }

    protected override async Task SendMessage()
    {
        void SendFriendMessage(MessageChain message, long target, long? quote = null)
        {
            var toSend = new
            {
                syncId  = -1,
                command = "sendFriendMessage",
                content = new
                {
                    target, quote,
                    messageChain = message.Messages.Select(m => m.ToObject()).ToList()
                }
            };
            _logger.Info($"({target,11}) <- {message}".Escape());
            _wsClient.Send(JsonSerializer.Serialize(toSend));
        }

        void SendGroupMessage(MessageChain message, long target, long? quote = null)
        {
            var toSend = new
            {
                syncId  = -1,
                command = "sendGroupMessage",
                content = new
                {
                    target, quote,
                    messageChain = message.Messages.Select(m => m.ToObject()).ToList()
                }
            };
            _logger.Info($"({target,11}) <= {message}".Escape());
            _wsClient.Send(JsonSerializer.Serialize(toSend));
        }

        void SendTempMessage(MessageChain message, long target, long? groupId, long? quote = null)
        {
            var toSend = new
            {
                syncId  = -1,
                command = "sendTempMessage",
                content = new
                {
                    qq    = target,
                    group = groupId,
                    quote,
                    messageChain = message.Messages.Select(m => m.ToObject()).ToList()
                }
            };
            _logger.Info($"({target,11}) <* {message}".Escape());
            _wsClient.Send(JsonSerializer.Serialize(toSend));
        }

        var taskList = new List<Task>();

        while (await MessageQueueProvider.SendQueue.Reader.WaitToReadAsync())
        {
            var s = await MessageQueueProvider.SendQueue.Reader.ReadAsync();

            switch (s.Type)
            {
                case MessageType.GroupMessage:
                    taskList.Add(Task.Run(() => SendGroupMessage(s.MessageChain, s.ReceiverId, s.QuoteId)));
                    break;
                case MessageType.FriendMessage:
                    taskList.Add(Task.Run(() => SendFriendMessage(s.MessageChain, s.ReceiverId, s.QuoteId)));
                    break;
                case MessageType.TempMessage:
                    taskList.Add(Task.Run(() => SendTempMessage(s.MessageChain, s.ReceiverId, s.GroupId, s.QuoteId)));
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

    public override async Task Invoke()
    {
        await _wsClient.Start();
        await base.Invoke();
    }
}
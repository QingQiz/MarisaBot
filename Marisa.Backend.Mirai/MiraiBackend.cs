using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using Flurl;
using log4net;
using Marisa.Backend.Mirai.MessageDataExt;
using Marisa.BotDriver.DI;
using Marisa.BotDriver.DI.Message;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Plugin;
using Marisa.Utils;
using Microsoft.Extensions.DependencyInjection;
using Websocket.Client;

namespace Marisa.Backend.Mirai;

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

    protected override Task RecvMessage()
    {
        async void OnMessage(ResponseMessage msg)
        {
            try
            {
                var message = msg.ToMessage(MessageSenderProvider);

                if (message == null) return;

                if (Environment.GetEnvironmentVariable("RESPONSE") == null)
                {
                    _logger.Info(message.GroupInfo == null
                        ? $"({message.Sender!.Id,11}) -> {message.MessageChain}".Escape()
                        : $"({message.GroupInfo.Id,11}) => ({message.Sender!.Id,11}) -> {message.MessageChain}".Escape());
                    await MessageQueueProvider.RecvQueue.Writer.WriteAsync(message);
                }
                else if (Environment.GetEnvironmentVariable("RESPONSE") == message.Sender!.Id.ToString())
                {
                    await MessageQueueProvider.RecvQueue.Writer.WriteAsync(message);
                }
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
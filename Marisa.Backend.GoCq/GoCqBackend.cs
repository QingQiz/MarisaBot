using System.ComponentModel;
using System.Text.Json;
using Marisa.Backend.GoCq.MessageDataExt;
using Marisa.BotDriver.DI;
using Marisa.BotDriver.DI.Message;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Plugin;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Websocket.Client;

namespace Marisa.Backend.GoCq;

public class GoCqBackend : BotDriver.BotDriver
{
    private readonly Logger _logger;
    private readonly WebsocketClient _wsClient;

    public GoCqBackend(
        IServiceProvider serviceProvider,
        IEnumerable<MarisaPluginBase> pluginsAll,
        DictionaryProvider dict,
        MessageSenderProvider messageSenderProvider,
        MessageQueueProvider messageQueueProvider
    ) : base(serviceProvider, pluginsAll, dict, messageSenderProvider, messageQueueProvider)
    {
        _logger = LogManager.GetCurrentClassLogger();
        string serverAddress = dict["ServerAddress"];

        _wsClient = new WebsocketClient(new Uri($"{serverAddress}"))
        {
            ReconnectTimeout = TimeSpan.MaxValue
        };

        _wsClient.ReconnectionHappened.Subscribe(_ => { _logger.Warn("Reconnection happened"); });
    }

    public new static IServiceCollection Config(Type[] types)
    {
        var sc = BotDriver.BotDriver.Config(types);
        sc.AddScoped(typeof(BotDriver.BotDriver), typeof(GoCqBackend));
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

                await MessageQueueProvider.RecvQueue.Writer.WriteAsync(message);
            }
            catch (Exception e)
            {
                _logger.Error(e + $"\ncaused by data: {msg.Text}");
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
                action = "send_msg",
                @params = new
                {
                    message_type = "private",
                    user_id      = target,
                    message      = (quote == null ? "" : $"[CQ:reply,id={quote}]") + MessageDataConverter.ToString(message)
                }
            };
            _logger.Info($"({target,11}) <- {message}".Escape());
            _wsClient.Send(JsonSerializer.Serialize(toSend));
        }

        void SendGroupMessage(MessageChain message, long target, long? quote = null)
        {
            var toSend = new
            {
                action = "send_msg",
                @params = new
                {
                    message_type = "group",
                    group_id     = target,
                    message      = (quote == null ? "" : $"[CQ:reply,id={quote}]") + MessageDataConverter.ToString(message)
                }
            };
            _logger.Info($"({target,11}) <= {message}".Escape());
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
using System.ComponentModel;
using Lagrange.Core;
using Lagrange.Core.Common;
using Lagrange.Core.Common.Interface;
using Lagrange.Core.Common.Interface.Api;
using Lagrange.Core.Message.Entity;
using Marisa.BotDriver.DI;
using Marisa.BotDriver.DI.Message;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Entity.MessageSender;
using Marisa.BotDriver.Plugin;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NLog;
using MessageBuilder = Lagrange.Core.Message.MessageBuilder;
using MessageChain = Lagrange.Core.Message.MessageChain;

namespace Marisa.Backend.Lagrange;

public class LagrangeBackend : BotDriver.BotDriver
{
    private readonly Logger _logger;

    private readonly BotDeviceInfo _deviceInfo = LoadOr("deviceInfo.json", BotDeviceInfo.GenerateInfo);
    private BotKeystore _keyStore = LoadOr("keystore.json", () => new BotKeystore());
    private readonly BotConfig _config = LoadOr("config.json", () => new BotConfig());
    private readonly BotContext _bot;

    private static readonly DateTime UnixDateTimeStart = new(1970, 1, 1, 0, 0, 0, 0);

    public LagrangeBackend(
        IServiceProvider serviceProvider,
        IEnumerable<MarisaPluginBase> pluginsAll,
        DictionaryProvider dict,
        MessageSenderProvider messageSenderProvider,
        MessageQueueProvider messageQueueProvider
    ) : base(serviceProvider, pluginsAll, dict, messageSenderProvider, messageQueueProvider)
    {
        _logger = LogManager.GetCurrentClassLogger();
        _bot    = BotFactory.Create(_config, _deviceInfo, _keyStore);
    }

    public new static IServiceCollection Config(Type[] types)
    {
        var sc = BotDriver.BotDriver.Config(types);
        sc.AddScoped<BotDriver.BotDriver, LagrangeBackend>();
        return sc;
    }

    protected override Task RecvMessage()
    {
        _bot.Invoker.OnFriendMessageReceived += async (_, msg) =>
        {
            _logger.Info(msg.Chain.ToPreviewString());
            var m = MessageChainConverter(msg.Chain);

            if (m is not null)
            {
                await MessageQueueProvider.RecvQueue.Writer.WriteAsync(m);
            }
        };

        _bot.Invoker.OnGroupMessageReceived += async (_, msg) =>
        {
            _logger.Info(msg.Chain.ToPreviewString());
            var m = MessageChainConverter(msg.Chain);

            if (m is not null)
            {
                await MessageQueueProvider.RecvQueue.Writer.WriteAsync(m);
            }
        };

        _bot.Invoker.OnGroupMuteEvent += async (_, @event) =>
        {
            await MessageQueueProvider.RecvQueue.Writer.WriteAsync(
                new Message(MessageSenderProvider,
                    @event.IsMuted ? new MessageDataBotMute(@event.GroupUin) : new MessageDataBotUnmute(@event.GroupUin))
                {
                    GroupInfo = new GroupInfo(@event.GroupUin, "", ""),
                    Sender    = new SenderInfo(0, ""),
                    Type      = MessageType.GroupMessage
                });
        };

        _bot.Invoker.OnGroupPokeEvent += async (_, @event) =>
        {
            await MessageQueueProvider.RecvQueue.Writer.WriteAsync(
                new Message(MessageSenderProvider, new MessageDataNudge(@event.TargetUin, @event.OperatorUin))
                {
                    GroupInfo = new GroupInfo(@event.GroupUin, "", ""),
                    Sender    = new SenderInfo(@event.OperatorUin, ""),
                    Type      = MessageType.GroupMessage
                });
        };

        _bot.Invoker.OnFriendPokeEvent += async (_, @event) =>
        {
            await MessageQueueProvider.RecvQueue.Writer.WriteAsync(
                new Message(MessageSenderProvider, new MessageDataNudge(@event.TargetUin, @event.OperatorUin))
                {
                    GroupInfo = null,
                    Sender    = new SenderInfo(@event.OperatorUin, ""),
                    Type      = MessageType.FriendMessage
                });
        };

        _bot.Invoker.OnGroupMemberDecreaseEvent += async (_, @event) =>
        {
            await MessageQueueProvider.RecvQueue.Writer.WriteAsync(
                new Message(MessageSenderProvider, new MessageDataMemberLeave(@event.MemberUin, "", @event.OperatorUin))
                {
                    GroupInfo = new GroupInfo(@event.GroupUin, "", ""),
                    Sender    = new SenderInfo(@event.MemberUin, ""),
                    Type      = MessageType.GroupMessage
                });
        };

        _bot.Invoker.OnGroupMemberIncreaseEvent += async (_, @event) =>
        {
            await MessageQueueProvider.RecvQueue.Writer.WriteAsync(
                new Message(MessageSenderProvider, new MessageDataNewMember(@event.MemberUin, @event.GroupUin, @event.InvitorUin))
                {
                    GroupInfo = new GroupInfo(@event.GroupUin, "", ""),
                    Sender    = new SenderInfo(@event.MemberUin, ""),
                    Type      = MessageType.GroupMessage
                });
        };
        return Task.CompletedTask;

        Message? MessageChainConverter(MessageChain chain)
        {
            var senderInfo = chain.FriendInfo is not null
                ? new SenderInfo(chain.FriendInfo.Uin, chain.FriendInfo.Nickname)
                : new SenderInfo(chain.GroupMemberInfo!.Uin, chain.GroupMemberInfo.MemberName);
            if (senderInfo.Id == _bot.BotUin) return null;

            var ds = new List<MessageData>
            {
                new MessageDataId(chain.Sequence, (long)(chain.Time - UnixDateTimeStart).TotalSeconds)
            };

            foreach (var entity in chain)
            {
                switch (entity)
                {
                    case FaceEntity:
                    case FileEntity:
                    case MultiMsgEntity:
                    case VideoEntity:
                    case XmlEntity:
                    case JsonEntity:
                        return null;
                    case ForwardEntity:
                        continue;
                    case ImageEntity img:
                        ds.Add(MessageDataImage.FromUrl(img.ImageUrl));
                        break;
                    case MentionEntity mention:
                        ds.Add(new MessageDataAt(mention.Uin));
                        break;
                    case TextEntity text:
                        ds.Add(new MessageDataText(text.Text));
                        break;
                }
            }

            return new Message(MessageSenderProvider, ds.ToArray())
            {
                GroupInfo = chain.GroupUin is not null
                    ? new GroupInfo((long)chain.GroupUin, "", chain.GroupMemberInfo!.Permission.ToString())
                    : null,
                Sender = senderInfo,
                Type   = chain.GroupUin is null ? MessageType.FriendMessage : MessageType.GroupMessage
            };
        }
    }

    protected override async Task SendMessage()
    {
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
        return;

        async Task SendGroupMessage(BotDriver.Entity.Message.MessageChain message, long target, long? quote = null)
        {
            var builder = MessageBuilder.Group((uint)target);
            var chain   = ConstructChain(quote, builder, message);
            _logger.Info(chain.ToPreviewString());
            _ = await _bot.SendMessage(chain);
        }

        async Task SendFriendMessage(BotDriver.Entity.Message.MessageChain message, long target, long? quote = null)
        {
            var builder = MessageBuilder.Friend((uint)target);
            var chain   = ConstructChain(quote, builder, message);
            _logger.Info(chain.ToPreviewString());
            _ = await _bot.SendMessage(chain);
        }

        MessageChain ConstructChain(long? quote, MessageBuilder builder, BotDriver.Entity.Message.MessageChain message)
        {
            // if (quote is not null)
            // {
            //     builder.Add(new ForwardEntity
            //     {
            //         Sequence = (uint)quote
            //     });
            // }

            foreach (var m in message.Messages)
            {
                switch (m)
                {
                    case MessageDataText text:
                        builder.Text(text.Text.ToString());
                        break;
                    case MessageDataImage image:
                        if (image.Base64 is not null)
                            builder.Image(Convert.FromBase64String(image.Base64));
                        else if (image.Path is not null)
                            builder.Image(File.ReadAllBytes(image.Path));
                        else if (image.Url is not null)
                            builder.Image(image.Url);
                        break;
                    case MessageDataAt at:
                        builder.Mention((uint)at.Target);
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
            return builder.Build();
        }
    }

    public override async Task Invoke()
    {
        var suc = await _bot.LoginByPassword();
        if (!suc)
        {
            var qrCode = await _bot.FetchQrCode();
            if (qrCode == null)
            {
                Console.WriteLine("null qrcode");
                return;
            }

            _logger.Info("qrcode dumped");
            // write qrcode to png
            await File.WriteAllBytesAsync("qrcode.png", qrCode.Value.QrCode);
            await _bot.LoginByQrCode();
        }

        _keyStore = _bot.UpdateKeystore();
        Dump("keystore.json", _keyStore);
        Dump("deviceInfo.json", _deviceInfo);
        Dump("config.json", _config);

        await base.Invoke();
    }

    private static T LoadOr<T>(string path, Func<T> factory)
    {
        if (File.Exists(path))
        {
            var data = JsonConvert.DeserializeObject<T>(File.ReadAllText(path))!;
            return data;
        }
        else
        {
            var data = factory();
            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(path, json);
            return data;
        }
    }

    private static void Dump<T>(string path, T data)
    {
        var json = JsonConvert.SerializeObject(data, Formatting.Indented);
        File.WriteAllText(path, json);
    }
}
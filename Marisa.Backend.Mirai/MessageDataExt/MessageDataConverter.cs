using System.Dynamic;
using Marisa.BotDriver.DI.Message;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Entity.MessageSender;
using Newtonsoft.Json;
using NLog;
using Websocket.Client;

namespace Marisa.Backend.Mirai.MessageDataExt;

public static class MessageDataConverter
{
    /// <summary>
    /// 将 <see cref="MessageData"/> 转换成 MiraiHttp 发送消息所需要的数据格式，方便后续转换为 json
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public static object ToObject(this MessageData data)
    {
        switch (data.Type)
        {
            case MessageDataType.Text:
                return new
                {
                    type = "Plain",
                    text = (data as MessageDataText)!.Text
                };
            case MessageDataType.At:
            {
                var m = (data as MessageDataAt)!;
                return new
                {
                    type    = "At",
                    target  = m.Target,
                    display = m.Display
                };
            }
            case MessageDataType.Image:
            {
                var m = data as MessageDataImage;

                return new
                {
                    type   = "Image",
                    base64 = m?.Base64,
                    url    = m?.Url,
                    path   = m?.Path
                };
            }
            case MessageDataType.Voice:
            {
                var d = (data as MessageDataVoice)!;

                return new
                {
                    voiceId = d.VoiceId,
                    url     = d.Url,
                    path    = d.Path,
                    base64  = d.Base64,
                    type    = "Voice"
                };
            }
            case MessageDataType.Id:
            case MessageDataType.Quote:
            case MessageDataType.AtAll:
            case MessageDataType.Face:
            case MessageDataType.FlashImage:
            case MessageDataType.Xml:
            case MessageDataType.Json:
            case MessageDataType.App:
            case MessageDataType.Nudge:
            case MessageDataType.Dice:
            case MessageDataType.MusicShare:
            case MessageDataType.Forward:
            case MessageDataType.File:
            case MessageDataType.MiraiCode:
            case MessageDataType.Unknown:
            case MessageDataType.NewMember:
            default:
                throw new NotImplementedException($"Converter for {data.Type} is not implemented");
        }
    }

    /// <summary>
    /// Mirai `Message` 类型的消息转化为 <see cref="Message"/>
    /// </summary>
    /// <param name="m"></param>
    /// <param name="ms"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    private static Message MessageToMessage(dynamic m, MessageSenderProvider ms)
    {
        var mds = new List<MessageData>();
        foreach (var md in m.messageChain)
        {
            switch (md.type)
            {
                case "Source":
                    mds.Add(new MessageDataId(md.id, md.time));
                    break;
                case "Plain":
                    mds.Add(new MessageDataText(md.text));
                    break;
                case "At":
                    mds.Add(new MessageDataAt(md.target, md.display));
                    break;
                case "Image":
                    mds.Add(MessageDataImage.FromUrl(md.url));
                    break;
                default:
                    mds.Add(new MessageDataUnknown());
                    break;
            }
        }

        var message = new Message(new MessageChain(mds), ms)
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
                new SenderInfo(m.sender.id, m.sender.memberName, null, m.sender.permission);
            message.GroupInfo =
                new GroupInfo(m.sender.group.id, m.sender.group.name, m.sender.group.permission);
        }

        return message;
    }

    /// <summary>
    /// Mirai `Event`类型的消息转化为 <see cref="Message"/>
    /// </summary>
    /// <param name="m"></param>
    /// <param name="ms"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    private static Message? EventToMessage(dynamic m, MessageSenderProvider ms)
    {
        switch (m.type)
        {
            case "NudgeEvent":
            {
                var message =
                    new Message(ms, new MessageDataNudge(m.target, m.fromId))
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
            case "MemberJoinEvent":
            {
                var md = new MessageDataNewMember(m.member.id, m.member.group.id, m.invitor?.id);
                var message = new Message(ms, md)
                {
                    Type      = MessageType.GroupMessage,
                    Sender    = new SenderInfo(md.Id, m.member.memberName, null, null),
                    GroupInfo = new GroupInfo(md.GroupId, m.member.group.name, null)
                };

                return message;
            }
            case "MemberLeaveEventKick":
            {
                var md = new MessageDataMemberLeave(m.member.id, m.member.memberName, m.@operator.id);
                var message = new Message(ms, md)
                {
                    Type      = MessageType.GroupMessage,
                    Sender    = new SenderInfo(md.Id, m.member.memberName, null, null),
                    GroupInfo = new GroupInfo(m.member.group.id, m.member.group.name, null)
                };
                return message;
            }
            case "MemberLeaveEventQuit":
            {
                var md = new MessageDataMemberLeave(m.member.id, m.member.memberName);
                var message = new Message(ms, md)
                {
                    Type      = MessageType.GroupMessage,
                    Sender    = new SenderInfo(md.Id, m.member.memberName, null, null),
                    GroupInfo = new GroupInfo(m.member.group.id, m.member.group.name, null)
                };
                return message;
            }
            case "GroupMuteAllEvent":
            {
                MessageData md;

                if (m.current)
                {
                    md = new MessageDataBotMute(m.group.id);
                }
                else
                {
                    md = new MessageDataBotUnmute(m.group.id);
                }

                return new Message(ms, md)
                {
                    Type      = MessageType.GroupMessage,
                    Sender    = new SenderInfo(m.@operator.id, m.@operator.memberName, null, null),
                    GroupInfo = new GroupInfo(m.group.id, m.group.name, null)
                };
            }
            case "BotMuteEvent":
            {
                var md = new MessageDataBotMute(m.@operator.group.id, m.durationSeconds);

                return new Message(ms, md)
                {
                    Type      = MessageType.GroupMessage,
                    Sender    = new SenderInfo(m.@operator.id, m.@operator.memberName, null, null),
                    GroupInfo = new GroupInfo(m.@operator.group.id, m.@operator.group.name, null)
                };
            }
            case "BotUnmuteEvent":
            {
                var md = new MessageDataBotUnmute(m.@operator.group.id);

                return new Message(ms, md)
                {
                    Type      = MessageType.GroupMessage,
                    Sender    = new SenderInfo(m.@operator.id, m.@operator.memberName, null, null),
                    GroupInfo = new GroupInfo(m.@operator.group.id, m.@operator.group.name, null)
                };
            }
        }

        return null;
    }

    /// <summary>
    /// 将 websocket 的接收消息转化为 <see cref="Message"/>
    /// </summary>
    /// <param name="msgIn"></param>
    /// <param name="ms"></param>
    /// <returns></returns>
    public static Message? ToMessage(this ResponseMessage msgIn, MessageSenderProvider ms)
    {
        var mExpando = JsonConvert.DeserializeObject<ExpandoObject>(msgIn.Text);
        var m        = (mExpando as dynamic)!.data;
        var mDict    = (m as IDictionary<string, object>)!;

        var logger = LogManager.GetCurrentClassLogger();

        if (mDict.ContainsKey("code"))
        {
            var code = mDict["code"].ToString();
            if (code != "0")
            {
                logger.Warn(mDict["msg"]);
            }

            return null;
        }

        if (m.type.Contains("Message"))
        {
            if (MessageToMessage(m, ms) is Message message)
            {
                return message;
            }
            else
            {
                logger.Warn($"Can not convert message `{msgIn.Text}` to Message");
            }
        }
        else if (m.type.Contains("Event")) // Event
        {
            if (EventToMessage(m, ms) is Message message)
            {
                return message;
            }
            else
            {
                logger.Warn($"Can not convert event `{msgIn.Text}` to Message");
            }
        }
        else
        {
            logger.Warn($"Unknown message {msgIn.Text}");
        }

        return null;
    }
}
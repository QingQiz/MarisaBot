using System.Dynamic;
using System.Text;
using log4net;
using Marisa.BotDriver.DI.Message;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Entity.MessageSender;
using Newtonsoft.Json;
using Websocket.Client;

namespace Marisa.Backend.GoCq.MessageDataExt;

public static class MessageDataConverter
{
    private static string EscapePar(this string s)
    {
        return s.Replace("[", "&#91;").Replace("]", "&#93;");
    }

    private static string Escape(this string s)
    {
        return s.Replace("&", "&#38;").Replace("[", "&#91;").Replace("]", "&#93;").Replace(",", "&#44;");
    }

    private static string Unescape(this string s)
    {
        return s.Replace("&amp;", "&").Replace("&#38;", "&").Replace("&#91;", "[").Replace("&#93;", "]").Replace("&#44;", ",");
    }

    private static MessageData? CqCodeToMessage(string raw)
    {
        if (raw.StartsWith("CQ:at,", StringComparison.OrdinalIgnoreCase))
        {
            return new MessageDataAt(long.Parse(raw["CQ:at,".Length ..].Split(',')[0].Split('=')[1]));
        }

        return null;
    }

    public static List<MessageData>? FromString(string messageRaw)
    {
        var res = new List<MessageData>();
        var l   = -1;
        var r   = 0;

        for (var i = 0; i < messageRaw.Length; i++)
        {
            if (messageRaw[i] == '[')
            {
                if (l != -1) return null;

                l = i;

                if (l > r)
                {
                    var msg = new MessageDataText(messageRaw[r..l].Unescape());
                    res.Add(msg);
                }
            }
            else if (messageRaw[i] == ']')
            {
                if (l == -1) return null;

                var msg = CqCodeToMessage(messageRaw[(l + 1)..i]);

                l = -1;
                r = i + 1;
                if (msg != null) res.Add(msg);
            }
        }

        if (r != messageRaw.Length)
        {
            var msg = new MessageDataText(messageRaw[r..].Unescape());
            res.Add(msg);
        }

        return res;
    }

    public static string ToString(this MessageChain mc)
    {
        var sb = new StringBuilder();

        foreach (var data in mc.Messages)
        {
            switch (data.Type)
            {
                case MessageDataType.Text:
                    sb.Append((data as MessageDataText)!.Text.EscapePar());
                    break;
                case MessageDataType.At:
                {
                    var m = (data as MessageDataAt)!;
                    sb.Append($"[CQ:at,qq={m.Target}]");
                    break;
                }
                case MessageDataType.Image:
                {
                    var m = (data as MessageDataImage)!;

                    if (m.Base64 != null)
                    {
                        sb.Append($"[CQ:image,file=base64://{m.Base64}]");
                    }
                    else if (m.Url != null)
                    {
                        sb.Append($"[CQ:image,file={m.Url.Escape()}]");
                    }
                    else if (m.Path != null)
                    {
                        sb.Append($"[CQ:image,file=file:///{m.Path.Escape()}]");
                    }

                    break;
                }
                case MessageDataType.Voice:
                {
                    var d = (data as MessageDataVoice)!;

                    if (d.Base64 != null)
                    {
                        sb.Append($"[CQ:record,file=base64://{d.Base64}]");
                    }
                    else if (d.Url != null)
                    {
                        sb.Append($"[CQ:record,file={d.Url}]");
                    }
                    else if (d.Path != null)
                    {
                        sb.Append($"[CQ:record,file=file:///{d.Path}]");
                    }

                    break;
                }
            }
        }

        return sb.ToString();
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
        var mc = new MessageChain(FromString(m.message).ToArray())
        {
            // Text = Unescape(m.message)
        };

        mc.Messages.Insert(0, new MessageDataId(m.message_id, m.time));

        var message = new Message(mc, ms)
        {
            Type = m.message_type switch
            {
                "group"   => MessageType.GroupMessage,
                "private" => MessageType.FriendMessage,
                _         => throw new ArgumentOutOfRangeException()
            }
        };

        if (message.Type == MessageType.FriendMessage && m.sub_type != "friend")
        {
            throw new NotSupportedException();
        }

        message.Sender = new SenderInfo(m.sender.user_id, m.sender.nickname);
        if (message.Type is MessageType.GroupMessage)
        {
            message.GroupInfo = new GroupInfo(m.group_id, "", "");
        }

        return message;
    }

    private static Message? EventToMessage(dynamic m, MessageSenderProvider ms)
    {
        var d = (m as IDictionary<string, object>)!;
        switch (m.notice_type)
        {
            case "notify":
            {
                if (m.sub_type == "poke")
                {
                    var message =
                        new Message(ms, new MessageDataNudge(m.target_id, m.user_id))
                        {
                            Type   = d.ContainsKey("group_id") ? MessageType.GroupMessage : MessageType.FriendMessage,
                            Sender = new SenderInfo(m.user_id, null, null, null),
                        };

                    if (message.Type == MessageType.GroupMessage)
                    {
                        message.GroupInfo = new GroupInfo(m.group_id, null, null);
                    }

                    return message;
                }
                return null;
            }
            case "group_increase":
            {
                var md = new MessageDataNewMember(m.user_id, m.group_id, m.sub_type == "approve" ? null : m.operator_id);
                var message = new Message(ms, md)
                {
                    Type      = MessageType.GroupMessage,
                    Sender    = new SenderInfo(md.Id, ""),
                    GroupInfo = new GroupInfo(md.GroupId, "", "")
                };

                return message;
            }
            case "group_decrease":
            {
                if (m.sub_type == "kick")
                {
                    var md = new MessageDataMemberLeave(m.member.id, m.member.memberName);
                    var message = new Message(ms, md)
                    {
                        Type      = MessageType.GroupMessage,
                        Sender    = new SenderInfo(md.Id, "", null, null),
                        GroupInfo = new GroupInfo(m.member.group.id, "", null)
                    };
                    return message;
                }

                if (m.sub_type == "leave")
                {
                    var md = new MessageDataMemberLeave(m.member.id, m.member.memberName, m.@operator.id);
                    var message = new Message(ms, md)
                    {
                        Type      = MessageType.GroupMessage,
                        Sender    = new SenderInfo(md.Id, "", null, null),
                        GroupInfo = new GroupInfo(m.member.group.id, "", null)
                    };
                    return message;
                }

                return null;
            }
            case "group_ban":
            {
                if (m.user_id == 0 || m.user_id == m.self_id)
                {
                    MessageData md;

                    if (m.sub_type)
                    {
                        md = new MessageDataBotMute(m.group.id, m.duration < 0 ? long.MaxValue : m.duration);
                    }
                    else
                    {
                        md = new MessageDataBotUnmute(m.group.id);
                    }

                    return new Message(ms, md)
                    {
                        Type      = MessageType.GroupMessage,
                        Sender    = new SenderInfo(m.operator_id, "", null, null),
                        GroupInfo = new GroupInfo(m.group.id, "", null)
                    };
                }

                return null;
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
        var m        = mExpando as dynamic;
        var d        = mExpando as IDictionary<string, object>;

        var logger = LogManager.GetLogger(nameof(MessageDataConverter));

        if (d.ContainsKey("retcode")) return null;
        if (m.post_type == "meta_event")
        {
            if (m.meta_event_type == "heartbeat")
            {
                return null;
            }
        }
        else if (m.post_type == "message")
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
        else if (m.post_type == "notice")
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
using System.Threading.Tasks.Dataflow;
using Flurl;
using Flurl.Http;
using QQBot.EntityFrameworkCore;
using QQBot.EntityFrameworkCore.Entity.Audit;
using QQBot.MiraiHttp.Entity;

namespace QQBot.MiraiHttp;

public partial class MiraiHttpSession
{
    private async Task RecvMessage()
    {
        async Task LogMessage(Message? message, dynamic m)
        {
            var log = new AuditLog
            {
                EventId   = Guid.NewGuid(),
                EventType = m.type,
                Time      = DateTime.Now,

                GroupName = message?.GroupInfo?.Name,
                GroupId   = message?.GroupInfo?.Id.ToString(),

                UserId    = message?.Sender?.Id.ToString(),
                UserName  = message?.Sender?.Name,
                UserAlias = message?.Sender?.Remark,
                Message   = Newtonsoft.Json.JsonConvert.SerializeObject(m.type.Contains("Message") ? m.messageChain : m)
            };

            await using var dbContext = new BotDbContext();
            await dbContext.Logs.AddAsync(log);
            await dbContext.SaveChangesAsync();

            Console.WriteLine(log.Message.Length < 120
                ? $"[{DateTime.Now:yyyy-MM-dd hh:mm:ss}][{m.type.PadLeft(15)}] {log.Message}"
                : $"[{DateTime.Now:yyyy-MM-dd hh:mm:ss}][{m.type.PadLeft(15)}] {log.Message[..120]}...");
        }

        var reportAddress = $"{_serverAddress}/countMessage";

        while (true)
        {
            var msgCnt = await reportAddress.SetQueryParam("sessionKey", _session).GetJsonAsync();
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

            Message? message = null;

            foreach (var m in msg.data)
            {
                if (m.type.Contains("Message"))
                {
                    message = new Message(new MessageChain(m.messageChain))
                    {
                        Type = m.type switch
                        {
                            "StrangerMessage" => MiraiMessageType.StrangerMessage,
                            "FriendMessage"   => MiraiMessageType.FriendMessage,
                            "GroupMessage"    => MiraiMessageType.GroupMessage,
                            "TempMessage"     => MiraiMessageType.TempMessage,
                            _                 => throw new ArgumentOutOfRangeException()
                        }
                    };


                    if (message.Type is MiraiMessageType.FriendMessage or MiraiMessageType.StrangerMessage)
                    {
                        message.Sender =
                            new MessageSenderInfo(m.sender.id, m.sender.nickname, m.sender.remark);
                    }
                    else
                    {
                        message.Sender =
                            new MessageSenderInfo(m.sender.id, m.sender.memberName, permission: m.sender.permission);
                        message.GroupInfo =
                            new GroupInfo(m.sender.group.id, m.sender.group.name, m.sender.group.permission);
                    }

                    _messageQueue.RecvQueue.Post(message);
                }
                else // Event
                {
                    OnEvent?.Invoke(this, m);
                }

                await LogMessage(message, m);
            }
        }
    }
}
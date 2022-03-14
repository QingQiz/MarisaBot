using System.Threading.Tasks.Dataflow;
using Flurl;
using Flurl.Http;
using QQBot.MiraiHttp.Entity;

namespace QQBot.MiraiHttp;

public partial class MiraiHttpSession
{
    private async Task RecvMessage()
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
                    var message = new Message(new MessageChain(m.messageChain))
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
            }
        }
    }
}
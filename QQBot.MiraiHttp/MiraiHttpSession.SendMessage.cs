using System.ComponentModel;
using System.Threading.Tasks.Dataflow;
using Flurl.Http;
using QQBot.MiraiHttp.Entity;

namespace QQBot.MiraiHttp
{
    public partial class MiraiHttpSession
    {
        private async Task SendMessage()
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
                        .Select(m => m.ConvertToObject()).ToList()
                };

                await sendFriendMessageAddress.PostJsonAsync((object)toSend);
            }

            async Task SendGroupMessage(MessageChain message, long target, long? quote = null)
            {
                dynamic toSend = new
                {
                    sessionKey = _session, target, quote,
                    messageChain = message.Messages
                        .Select(m => m.ConvertToObject()).ToList()
                };

                await sendGroupMessageAddress.PostJsonAsync((object)toSend);
            }

            async Task SendTempMessage(MessageChain message, long target, long? groupId, long? quote = null)
            {
                dynamic toSend = new
                {
                    sessionKey = _session,
                    qq = target,
                    group = groupId,
                    quote,
                    messageChain = message.Messages
                        .Select(m => m.ConvertToObject()).ToList()
                };

                await sendTempMessageAddress.PostJsonAsync((object)toSend);
            }

            var taskList = new List<Task>();

            while (await _messageQueue.SendQueue.OutputAvailableAsync())
            {
                var (message, type, target, groupId, quote) = await _messageQueue.SendQueue.ReceiveAsync();

                switch (type)
                {
                    case MiraiMessageType.GroupMessage:
                        taskList.Add(SendGroupMessage(message, target, quote));
                        break;
                    case MiraiMessageType.FriendMessage:
                        taskList.Add(SendFriendMessage(message, target, quote));
                        break;
                    case MiraiMessageType.TempMessage:
                        taskList.Add(SendTempMessage(message, target, groupId, quote));
                        break;
                    case MiraiMessageType.StrangerMessage:
                        throw new NotImplementedException();
                    default:
                        throw new InvalidEnumArgumentException();
                }

                if (taskList.Count >= 100)
                {
                    await Task.WhenAll(taskList);
                    taskList.Clear();
                }
            }

            await Task.WhenAll(taskList);
        }
    }
}
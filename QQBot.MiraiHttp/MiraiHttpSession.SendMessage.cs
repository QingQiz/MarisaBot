using System.ComponentModel;
using System.Threading.Tasks.Dataflow;
using Flurl.Http;
using QQBot.MiraiHttp.Entity;

namespace QQBot.MiraiHttp
{
    public partial class MiraiHttpSession
    {
        private async Task SendFriendMessage(MessageChain message, long target, long? quote = null)
        {
            dynamic toSend = new
            {
                sessionKey = _session,
                target     = target,
                quote      = quote,
                messageChain = message.Messages
                    .Select(m => m.ConvertToObject()).ToList()
            };

            await $"{_serverAddress}/sendFriendMessage".PostJsonAsync((object)toSend);
        }

        private async Task SendGroupMessage(MessageChain message, long target, long? quote = null)
        {
            dynamic toSend = new
            {
                sessionKey = _session,
                target     = target,
                quote      = quote,
                messageChain = message.Messages
                    .Select(m => m.ConvertToObject()).ToList()
            };

            await $"{_serverAddress}/sendGroupMessage".PostJsonAsync((object)toSend);
        }

        private async Task SendMessage()
        {
            var taskList = new List<Task>();

            while (await _messageQueue.SendQueue.OutputAvailableAsync())
            {
                var (message, type, target, quote) = await _messageQueue.SendQueue.ReceiveAsync();

                switch (type)
                {
                    case MiraiMessageType.GroupMessage:
                        taskList.Add(SendGroupMessage(message, target, quote));
                        break;
                    case MiraiMessageType.FriendMessage:
                        taskList.Add(SendFriendMessage(message, target, quote));
                        break;
                    case MiraiMessageType.TempMessage:
                        throw new NotImplementedException();
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
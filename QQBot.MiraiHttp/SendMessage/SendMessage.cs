using Flurl.Http;
using QQBot.MiraiHttp.Entity;

namespace QQBot.MiraiHttp
{
    public partial class MiraiHttpSession
    {
        public async Task SendFriendMessage(Message message, long target, long? quote = null)
        {
            dynamic toSend = new
            {
                sessionKey = _session,
                target     = target,
                quote      = quote,
                messageChain = message.MessageChain?.Messages
                    .Select(m => m.ConvertToObject()).ToList()
            };

            await $"{_serverAddress}/sendFriendMessage".PostJsonAsync((object)toSend);
        }

        public async Task SendGroupMessage(Message message, long target, long? quote = null)
        {
            dynamic toSend = new
            {
                sessionKey = _session,
                target     = target,
                quote      = quote,
                messageChain = message.MessageChain?.Messages
                    .Select(m => m.ConvertToObject()).ToList()
            };

            await $"{_serverAddress}/sendGroupMessage".PostJsonAsync((object)toSend);
        }
    }
}
using System.Linq;
using System.Threading.Tasks;
using Flurl.Http;
using QQBOT.Core.MiraiHttp.Entity;

namespace QQBOT.Core.MiraiHttp
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
using System.Linq;
using System.Threading.Tasks;
using Flurl.Http;
using QQBOT.Core.Attribute;
using QQBOT.Core.MiraiHttp;
using QQBOT.Core.MiraiHttp.Entity;
using QQBOT.Core.Plugin.PluginEntity.MaiMaiDx;
using QQBOT.Core.Util;

namespace QQBOT.Core.Plugin
{
    [MiraiPlugin]
    public class MaiMaiDx: PluginBase
    {
        private static readonly string[] CommandPrefix = { "maimai", "mai", "舞萌" };
        private static readonly string[] SubCommand = { "b40", "search", "搜索" , "查分", "搜歌"};

        private static async Task<DxRating> MaiB40(object sender)
        {

            var response = await "https://www.diving-fish.com/api/maimaidxprober/query/player".PostJsonAsync(sender);
            var json = await response.GetJsonAsync();
            
            var rating = new DxRating(json);
            
            return rating;
        }
        
        private async Task<MessageChain> Handler(string msg, MessageSenderInfo sender)
        {
            msg = msg.TrimStart(CommandPrefix);

            if (string.IsNullOrEmpty(msg)) return null;

            var res = msg.CheckPrefix(SubCommand);

            foreach (var (prefix, index) in res)
            {
                switch (index)
                {
                    case 0:
                    case 3:
                        var username = msg.TrimStart(prefix).Trim();

                        try
                        {
                            var rating = await MaiB40(string.IsNullOrEmpty(username)
                                ? new { qq = sender.Id }
                                : new { username });

                            var imgB64 = rating.GetImage();


                            return new MessageChain(new[]
                            {
                                ImageMessage.FromBase64(imgB64)
                            });
                        }
                        catch (FlurlHttpException e)
                        {
                            return new MessageChain(new[]
                            {
                                new PlainMessage(e.StatusCode == 400 ? "“查无此人”" : $"BOT在差你分的过程中炸了：\n{e}")
                            });
                        }
                    case 1:
                    case 2:
                    case 4:
                        return new MessageChain(new[]
                        {
                            new PlainMessage("搜你马呢")
                        });
                }
            }

            return null;
        }
        
        public override async Task FriendMessageHandler(MiraiHttpSession session, Message message)
        {
            var msg = message.MessageChain!.PlainText;
            var mc = await Handler(msg, message.Sender);

            if (mc == null) return;

            await session.SendFriendMessage(new Message(mc), message.Sender!.Id);
        }

        public override async Task GroupMessageHandler(MiraiHttpSession session, Message message)
        {
            var msg = message.MessageChain!.PlainText;
            var mc = await Handler(msg, message.Sender);

            if (mc == null) return;

            var source = (message.MessageChain.Messages.First(m => m.Type == MessageType.Source) as SourceMessage)!.Id;

            await session.SendGroupMessage(new Message(mc), message.GroupInfo!.Id, source);
        }
    }
}
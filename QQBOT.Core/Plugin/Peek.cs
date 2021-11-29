using System.Drawing;
using System.Threading.Tasks;
using QQBOT.Core.MiraiHttp;
using QQBOT.Core.MiraiHttp.Entity;
using QQBOT.Core.Plugin.PluginEntity;
using QQBOT.Core.Util;

namespace QQBOT.Core.Plugin
{
    public class Peek : PluginBase
    {
        private string CaptureScreen(bool hide=true)
        {
            const int w = 1440;
            const int h = 810;

            var bitmap = new Bitmap(2560, 1440);

            using (var g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(0, 0, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
            }

            return hide
                ? bitmap.Resize(w, h).Blur(new Rectangle(0, 0, w, h), 4).ToB64()
                : bitmap.ToB64();
        }

        protected override async Task<PluginTaskState> GroupMessageHandler(MiraiHttpSession session, Message message)
        {
            if (message.MessageChain!.PlainText.Trim() == ":peek" && message.At(session.Id))
            {
                await session.SendGroupMessage(new Message(MessageChain.FromBase64(CaptureScreen())),
                    message.GroupInfo!.Id, message.Source.Id);
                return PluginTaskState.CompletedTask;
            }

            return PluginTaskState.NoResponse;
        }

        protected override async Task<PluginTaskState> FriendMessageHandler(MiraiHttpSession session, Message message)
        {
            if (message.MessageChain!.PlainText.Trim() == ":peek")
            {
                await session.SendFriendMessage(new Message(MessageChain.FromBase64(CaptureScreen(hide: message.Sender!.Id != 642191352L))),
                    message.Sender!.Id);
                return PluginTaskState.CompletedTask;
            }

            return PluginTaskState.NoResponse;
        }
    }
}
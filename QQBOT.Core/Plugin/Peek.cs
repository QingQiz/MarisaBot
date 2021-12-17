using System.Collections.Generic;
using System.Drawing;
using QQBOT.Core.MiraiHttp;
using QQBOT.Core.MiraiHttp.Entity;
using QQBOT.Core.Util;

namespace QQBOT.Core.Plugin
{
    public class Peek : PluginBase
    {
        private bool _peekEnabled = true;

        private string CaptureScreen(bool hide = true)
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

#pragma warning disable 1998
        protected override async IAsyncEnumerable<MessageChain> MessageHandler(Message message, MiraiMessageType type)
        {
            const long authorId = 642191352L;

            var msg      = message.MessageChain!.PlainText.Trim();
            var senderId = message.Sender!.Id;

            if (!msg.StartsWith(":peek")) yield return null;

            switch (msg.TrimStart(":peek").Trim())
            {
                // disable peek
                case "0" when senderId == authorId:
                    _peekEnabled = false;
                    yield return MessageChain.FromPlainText("peek disabled");
                    break;
                case "1" when senderId == authorId:
                    _peekEnabled = true;
                    yield return MessageChain.FromPlainText("peek enabled");
                    break;
                case "" when _peekEnabled:
                    yield return MessageChain.FromBase64(CaptureScreen(senderId != authorId));
                    break;
                default:
                    yield return MessageChain.FromPlainText("Denied");
                    break;
            }
        }
#pragma warning restore 1998
    }
}
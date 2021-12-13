using System.Collections.Generic;
using QQBOT.Core.Attribute;
using QQBOT.Core.MiraiHttp;
using QQBOT.Core.MiraiHttp.Entity;
using QQBOT.Core.Util;

namespace QQBOT.Core.Plugin.Arcaea
{
    [MiraiPlugin]
    public partial class Arcaea: PluginBase
    {
        protected override async IAsyncEnumerable<MessageChain> MessageHandler(Message message, MiraiMessageType type)
        {
            string[] commandPrefix = { "arcaea", "arc", "阿卡伊" };
            string[] subCommand =
                {
                //    0      1       2        3      4      5
                    "猜歌", "猜曲", "alias", "别名", "song", "id"
                };

            var sender = message.Sender;
            var msg    = message.MessageChain!.PlainText.Trim().TrimStart(commandPrefix);

            
            if (string.IsNullOrEmpty(msg)) yield return null;

            var prefixes = msg.CheckPrefix(subCommand);

            foreach (var (prefix, index) in prefixes)
            {
                switch (index)
                {
                    case 0:
                    case 1: // 猜歌
                    {
                        var param = msg.TrimStart(prefix).Trim();
                        if (message.GroupInfo == null)
                            yield return MessageChain.FromPlainText("仅群组中使用");

                        switch (param)
                        {
                            case "":
                                yield return StartSongCoverGuess(message);
                                break;
                        }

                        yield return null;
                        break;
                    }
                    case 2:
                    case 3: // alias
                    {
                        var param = msg.TrimStart(prefix).Trim();
                        yield return SongAliasHandler(param);
                        break;
                    }
                    case 4: // search
                    {
                        var name   = msg.TrimStart(prefix).Trim();
                        var search = SearchSongByAlias(name);
                        yield return GetSearchResult(search);
                        break;
                    }
                    case 5: // id
                    {
                        var last = msg.TrimStart(prefix).Trim();

                        yield return long.TryParse(last, out var id)
                            ? GetSongInfo(id)
                            : MessageChain.FromPlainText("“你看你输的这个几把玩意儿像不像个ID”");
                        break;
                    }
                }
            }

            yield return null;
        }
    }
}
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace QQBot.MiraiHttp.Entity.MessageData;

[JsonConverter(typeof(StringEnumConverter))]
public enum MessageType
{
    Source,
    Quote,
    At,
    AtAll,
    Face,
    Plain,
    Image,
    FlashImage,
    Voice,
    Xml,
    Json,
    App,
    Poke,
    Dice,
    MusicShare,
    Forward,
    File,
    MiraiCode
}
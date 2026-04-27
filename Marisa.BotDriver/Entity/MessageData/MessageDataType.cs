using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Marisa.BotDriver.Entity.MessageData;

[JsonConverter(typeof(StringEnumConverter))]
public enum MessageDataType
{
    // Message
    Id,
    Quote,
    At,
    AtAll,
    Face,
    Text,
    Image,
    FlashImage,
    Voice,
    Video,
    Xml,
    Json,
    App,
    LightApp,
    Nudge,
    Poke,
    Dice,
    Rps,
    Shake,
    Share,
    Contact,
    Location,
    MusicShare,
    Forward,
    Node,
    File,
    MFace,
    Markdown,
    MiraiCode,
    OneBotSegment,
    // Event
    NewMember,
    MemberLeave,
    BotMute,
    BotUnmute,
    BotOffline,
    BotOnline,
    OneBotEvent,
    // others
    Unknown
}

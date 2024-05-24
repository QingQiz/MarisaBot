using System.Text.Json.Serialization;
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
    Xml,
    Json,
    App,
    Nudge,
    Dice,
    MusicShare,
    Forward,
    File,
    MiraiCode,
    // Event
    NewMember,
    MemberLeave,
    BotMute,
    BotUnmute,
    BotOffline,
    BotOnline,
    // others
    Unknown
}
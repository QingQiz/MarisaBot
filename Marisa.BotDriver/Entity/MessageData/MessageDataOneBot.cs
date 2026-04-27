namespace Marisa.BotDriver.Entity.MessageData;

/// <summary>
/// Exact OneBot/NapCat message segment for segment types that Marisa does not model with a dedicated class.
/// Use <see cref="SegmentType"/> as the outgoing OneBot segment type and <see cref="Data"/> as its data object.
/// This keeps full NapCat segment coverage without forcing every plugin to depend on NapCat-specific classes.
/// </summary>
public class MessageDataOneBotSegment : MessageData
{
    public MessageDataOneBotSegment(string segmentType, Dictionary<string, object?> data, MessageDataType type = MessageDataType.OneBotSegment)
    {
        SegmentType = segmentType;
        Data = data;
        Type = type;
    }

    public string SegmentType { get; }

    public Dictionary<string, object?> Data { get; }

    public override MessageDataType Type { get; }
}

/// <summary>
/// Exact OneBot/NapCat event payload for events that are not part of Marisa's historical semantic event model.
/// <see cref="EventName"/> is normalized as post_type.event_type.sub_type, e.g. notice.group_ban.ban.
/// </summary>
public class MessageDataOneBotEvent : MessageData
{
    public MessageDataOneBotEvent(string postType, string eventName, Dictionary<string, object?> data, string? eventType = null, string? subType = null)
    {
        PostType = postType;
        EventName = eventName;
        EventType = eventType;
        SubType = subType;
        Data = data;
    }

    public string PostType { get; }

    public string EventName { get; }

    public string? EventType { get; }

    public string? SubType { get; }

    public Dictionary<string, object?> Data { get; }

    public override MessageDataType Type => MessageDataType.OneBotEvent;
}

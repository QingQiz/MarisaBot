namespace QQBot.MiraiHttp.Entity.MessageData;

public class SourceMessage : MessageData
{
    public long Id;
    public long Time;

    public SourceMessage(long id, long time)
    {
        Id   = id;
        Time = time;
        Type = MessageType.Source;
    }
}
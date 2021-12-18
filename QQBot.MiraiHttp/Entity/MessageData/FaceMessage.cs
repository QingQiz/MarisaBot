namespace QQBot.MiraiHttp.Entity.MessageData;

public class FaceMessage : MessageData
{
    public long FaceId;
    public string Name;

    public FaceMessage(long faceId, string name)
    {
        FaceId = faceId;
        Name   = name;
    }
}
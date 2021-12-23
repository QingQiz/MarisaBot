namespace QQBot.MiraiHttp
{
    [Flags]
    public enum MiraiMessageType
    {
        GroupMessage    = 1 << 0,
        FriendMessage   = 1 << 1,
        TempMessage     = 1 << 2,
        StrangerMessage = 1 << 3
    }
}
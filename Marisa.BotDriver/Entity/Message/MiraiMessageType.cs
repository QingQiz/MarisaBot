namespace Marisa.BotDriver.Entity.Message;

[Flags]
public enum MessageType
{
    GroupMessage    = 1 << 0,
    FriendMessage   = 1 << 1,
    TempMessage     = 1 << 2,
    StrangerMessage = 1 << 3
}
namespace Marisa.BotDriver.Entity.MessageSender;

public record SenderInfo(long Id, string Name, string? Remark = null, string? Permission = null);
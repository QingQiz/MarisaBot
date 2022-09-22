using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Plugin;

namespace Marisa.Plugin.Shared.Dialog;

public static class Dialog
{
    public delegate Task<MarisaPluginTaskState> MessageHandler(Message message);
    public delegate bool MessageHandlerAdder(long? group, long? id, MessageHandler handler);
}
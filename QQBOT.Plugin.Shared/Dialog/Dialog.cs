using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Plugin;

namespace QQBot.Plugin.Shared.Dialog;

public static class Dialog
{
    public delegate Task<MiraiPluginTaskState> MessageHandler(MessageSenderProvider ms, Message message);
    public delegate bool MessageHandlerAdder(long id, MessageHandler handler);
}
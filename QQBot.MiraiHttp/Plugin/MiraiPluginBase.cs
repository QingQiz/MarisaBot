namespace QQBot.MiraiHttp.Plugin;

[MiraiPlugin]
[MiraiPluginDisabled]
public abstract class MiraiPluginBase
{
    public virtual async Task EventHandler(MiraiHttpSession session, dynamic data)
    {
        await Task.CompletedTask;
    }
}
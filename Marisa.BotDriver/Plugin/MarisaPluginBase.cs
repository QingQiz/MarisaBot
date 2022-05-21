using Marisa.BotDriver.Plugin.Attributes;

namespace Marisa.BotDriver.Plugin;

[MarisaPlugin]
public class MarisaPluginBase
{
    public virtual Task BackgroundService()
    {
        return Task.CompletedTask;
    }
}
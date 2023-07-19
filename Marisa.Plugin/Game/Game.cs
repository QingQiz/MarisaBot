using System.Diagnostics.CodeAnalysis;

namespace Marisa.Plugin.Game;

[MarisaPlugin(PluginPriority.Game)]
[MarisaPluginDoc("一些小游戏")]
[MarisaPluginCommand(":game", "：game")]
[SuppressMessage("ReSharper", "UnusedMember.Local")]
public partial class Game : PluginBase
{
}
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using Marisa.EntityFrameworkCore;
using Marisa.EntityFrameworkCore.Entity.Plugin.Chunithm;
using Marisa.Plugin.Shared.Chunithm;
using Marisa.Plugin.Shared.Util.SongDb;
using Newtonsoft.Json;

namespace Marisa.Plugin.Chunithm;

[MarisaPlugin(PluginPriority.Chunithm)]
[MarisaPluginDoc("音游 Chunithm 的相关功能")]
[MarisaPluginCommand("chunithm", "chu", "中二")]
[SuppressMessage("ReSharper", "UnusedMember.Local")]
public partial class Chunithm : MarisaPluginBase
{
    private readonly SongDb<ChunithmSong, ChunithmGuess> _songDb = new(
        ResourceManager.ResourcePath + "/aliases.tsv",
        ResourceManager.TempPath     + "/ChunithmSongAliasTemp.txt",
        () =>
        {
            var data = JsonConvert.DeserializeObject<ExpandoObject[]>(
                File.ReadAllText(ResourceManager.ResourcePath + "/SongInfo.json")
            ) as dynamic[];
            return data!.Select(d => new ChunithmSong(d)).ToList();
        },
        nameof(BotDbContext.ChunithmGuesses),
        Dialog.AddHandler
    );
}
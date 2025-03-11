using Marisa.EntityFrameworkCore;
using Marisa.EntityFrameworkCore.Entity.Plugin.Ongeki;
using Marisa.Plugin.Shared.Interface;
using Marisa.Plugin.Shared.Ongeki;
using Marisa.Plugin.Shared.Util.SongDb;
using Marisa.Plugin.Shared.Util.SongGuessMaker;
using Newtonsoft.Json;

namespace Marisa.Plugin.Ongeki;

[MarisaPlugin(PluginPriority.Chunithm)]
[MarisaPluginDoc("音游 Ongeki 的相关功能")]
[MarisaPluginCommand("ongeki", "ogk", "音击")]
public partial class Ongeki :
    MarisaPluginBase,
    IMarisaPluginWithHelp,
    IMarisaPluginWithRetrieve<OngekiSong>,
    IMarisaPluginWithCoverGuess<OngekiSong, OngekiGuess>
{
    public Ongeki()
    {
        SongDb = new SongDb<OngekiSong>(
            ResourceManager.ResourcePath + "/aliases.tsv",
            ResourceManager.TempPath + "/OngekiSongAliasTemp.txt",
            () =>
            {
                var data = JsonConvert.DeserializeObject<OngekiMusicDataRecord[]>(
                    File.ReadAllText(ResourceManager.ResourcePath + "/ongeki.json")
                ) as dynamic[];
                return data!.Select(d => new OngekiSong(d)).ToList();
            },
            Dialog.TryAddHandler
        );

        SongGuessMaker = new SongGuessMaker<OngekiSong, OngekiGuess>(SongDb, nameof(BotDbContext.OngekiGuesses));
    }

    public SongGuessMaker<OngekiSong, OngekiGuess> SongGuessMaker { get; }

    public SongDb<OngekiSong> SongDb { get; }
}
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Marisa.Database.Entity.Plugin.Shared;
using Realms;

namespace Marisa.Database.Entity.Plugin.MaiMaiDx;

[Table("MaiMaiDx.Guess")]
public partial class MaiMaiDxGuess : IRealmObject, ISongGuess
{
    [Key]
    [PrimaryKey]
    public long Id { get; set; }

    [Indexed]
    public long UId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int TimesStart { get; set; }

    public int TimesCorrect { get; set; }

    public int TimesWrong { get; set; }

    public MaiMaiDxGuess()
    {
    }

    public MaiMaiDxGuess(long uid, string name)
    {
        UId = uid;
        Name = name;
    }
}

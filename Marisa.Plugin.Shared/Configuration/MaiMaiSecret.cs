#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
namespace Marisa.Plugin.Shared.Configuration;

public class MaiMaiSecret
{
    public string MaiSalt { get; set; }
    public string AimeSalt { get; set; }

    public string AesKey {get;set;}
    public string AesIv { get; set; }
}
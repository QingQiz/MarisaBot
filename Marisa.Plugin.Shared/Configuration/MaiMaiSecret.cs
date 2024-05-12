namespace Marisa.Plugin.Shared.Configuration;

public class MaiMaiSecret
{
    public string MaiSalt { get; set; }
    public string AimeSalt { get; set; }

    public string AesKey {get;set;}
    public string AesIv { get; set; }
}
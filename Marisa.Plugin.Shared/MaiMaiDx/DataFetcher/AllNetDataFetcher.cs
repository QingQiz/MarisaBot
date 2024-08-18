﻿using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Flurl.Http;
using Marisa.EntityFrameworkCore;
using Marisa.Plugin.Shared.Configuration;
using Marisa.Plugin.Shared.Util.SongDb;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using Polly;
using Polly.Retry;

namespace Marisa.Plugin.Shared.MaiMaiDx.DataFetcher;

public class AllNetDataFetcher(SongDb<MaiMaiSong> songDb) : DataFetcher(songDb)
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static AsyncRetryPolicy GetPolicy(string actionName)
    {
        return Policy
            .Handle<FlurlHttpException>(e => e.StatusCode == 404)
            .WaitAndRetryAsync(
                10,
                _ => TimeSpan.FromSeconds(1),
                (exception, timeSpan, retryCount, _) =>
                {
                    Logger.Error($"Failed: {actionName}, retry: {retryCount}, sleep: {timeSpan.Seconds} seconds, because: {exception.Message}");
                }
            );
    }

    public override async Task<DxRating> GetRating(Message message)
    {
        var id = GetAimeId(message);

        var scores = await GetPolicy("GetScores").ExecuteAsync(async () => await GetScores(id));

        var group = scores
            .GroupBy(x => SongDb.SongIndexer[x.Key.Id].Info.IsNew)
            .ToList();

        var b35 = group.FirstOrDefault(x => !x.Key)?
                      .OrderByDescending(x => x.Value.Rating)
                      .ThenByDescending(x => x.Value.Id)
                      .Take(35)
                      .Select(x => x.Value)
                      .ToList()
               ?? [];

        var b15 = group
                      .FirstOrDefault(x => x.Key)?
                      .OrderByDescending(x => x.Value.Rating)
                      .ThenByDescending(x => x.Value.Id)
                      .Take(15)
                      .Select(x => x.Value)
                      .ToList()
               ?? [];

        return new DxRating
        {
            Nickname  = (await GetUserPreview(id)).Username,
            OldScores = b35,
            NewScores = b15
        };
    }

    public override async Task<Dictionary<(long Id, int LevelIdx), SongScore>> GetScores(Message message)
    {
        var id = GetAimeId(message);

        return await GetPolicy("GetScores").ExecuteAsync(async () => await GetScores(id));
    }

    public static async Task<bool> Logout(int userId)
    {
        var req = JsonConvert.SerializeObject(new { userId });

        var rep = await GetPolicy("UserLogoutApiMaimaiChn")
            .ExecuteAsync(async () => await MakeMaiRequest("UserLogoutApiMaimaiChn", req, userId));

        var res = Encoding.UTF8.GetString(AesDecrypt(await Decompress(rep)));
        return JsonConvert.DeserializeObject<Dictionary<string, int>>(res)!["returnCode"] == 1;
    }

    public static async Task Fetch(int aimeId)
    {
        var md = await GetMusicData(aimeId);

        await File.WriteAllTextAsync(
            Path.Join(ConfigurationManager.Configuration.MaiMai.TempPath, $"UserMusicData-{aimeId}.json"),
            JsonConvert.SerializeObject(md)
        );
    }

    private async Task<Dictionary<(long Id, int LevelIndex), SongScore>> GetScores(int aimeId)
    {
        var cache = Path.Join(ConfigurationManager.Configuration.MaiMai.TempPath, $"UserMusicData-{aimeId}.json");

        if (!File.Exists(cache)) await Fetch(aimeId);

        var md = JsonConvert.DeserializeObject<List<MusicData>>(await File.ReadAllTextAsync(Path.Join(cache)))!;

        var ret = new Dictionary<(long Id, int LevelIndex), SongScore>();

        foreach (var data in md)
        {
            var exist = SongDb.SongIndexer.TryGetValue(data.Id, out var song);

            if (!exist || song == null) continue;

            var levelIndex = data.LevelIndex - (data.LevelIndex >= 10 ? 10 : 0);

            ret[(data.Id, levelIndex)] = new SongScore
            {
                Id          = data.Id,
                Achievement = data.Achievement,
                Fc          = data.Combo switch { 1 => "fc", 2 => "fcp", 3 => "ap", 4 => "app", _ => "" },
                LevelIdx    = levelIndex,
                Title       = song.Title,
                Constant    = song.Constants[levelIndex],
                DxScore     = data.DxScore,
                Fs          = data.Sync switch { 1 => "fs", 2 => "fsp", 3 => "fsd", 4 => "fsdp", _ => "" },
                Level       = song.Levels[levelIndex],
                Type        = song.Type
            };
        }

        return ret;
    }

    private static int GetAimeId(Message message)
    {
        var qq = message.Sender.Id;

        var at = message.MessageChain!.Messages.FirstOrDefault(m => m.Type == MessageDataType.At);
        if (at != null)
        {
            qq = (at as MessageDataAt)?.Target ?? qq;
        }

        using var db = new BotDbContext();

        var user = db.MaiMaiBinds.First(x => x.UId == qq);

        return user.AimeId;
    }

    public static async Task<int> GetUserId(ReadOnlyMemory<char> qrCodeResult)
    {
        if (!qrCodeResult[..4].Equals(WeChatId, StringComparison.Ordinal)
         || !qrCodeResult[4..8].Equals(GameId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("无效的");
        }

        var timestamp = qrCodeResult[8..20];
        var qrCode    = qrCodeResult[20..];

        var chipId = $"A63E-01E{Random.Shared.Next(999999999).ToString().PadLeft(8, '0')}";

        var key = BitConverter.ToString(
            SHA256.HashData(Encoding.UTF8.GetBytes($"{chipId}{timestamp}{AimeSalt}"))
        ).Replace("-", "").ToUpper();

        var data = JsonConvert.SerializeObject(new
        {
            chipID     = chipId,
            openGameID = GameId,
            key,
            qrCode,
            timestamp
        });

        var rep = await $"http://{AimeHost}/wc_aime/api/get_data"
            .WithHeaders(new
            {
                Host           = AimeHost,
                User_Agent     = "WC_AIME_LIB",
                Content_Length = data.Length
            })
            .PostStringAsync(data);

        var result = await rep.GetJsonAsync<UserIdRep>();

        if (result.errorID != 0)
        {
            throw new InvalidDataException("获取ID失败，错误码：" + result.errorID);
        }

        return result.userID;
    }


    #region Helper

    private static readonly string MaiSalt = ConfigurationManager.Configuration.MaiMai.Secret.MaiSalt;

    private const string MaiHost = "maimai-gm.wahlap.com:42081";

    private const string AimeHost = "ai.sys-all.cn";
    private static readonly string AimeSalt = ConfigurationManager.Configuration.MaiMai.Secret.AimeSalt;

    private const string WeChatId = "SGWC";
    private const string GameId = "MAID";

    private static readonly string AesKey = ConfigurationManager.Configuration.MaiMai.Secret.AesKey;
    private static readonly string AesIv = ConfigurationManager.Configuration.MaiMai.Secret.AesIv;

    // ReSharper disable InconsistentNaming
    // ReSharper disable once ClassNeverInstantiated.Local
    private record UserIdRep(int errorID, int userID);
    // ReSharper restore InconsistentNaming

    private static byte[] AesEncrypt(string data)
    {
        var key = Encoding.UTF8.GetBytes(AesKey);
        var iv  = Encoding.UTF8.GetBytes(AesIv);

        using var aes = Aes.Create();
        aes.Mode    = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key     = key;
        aes.IV      = iv;

        var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

        var inputBytes     = Encoding.UTF8.GetBytes(data);
        var encryptedBytes = encryptor.TransformFinalBlock(inputBytes, 0, inputBytes.Length);

        return encryptedBytes;
    }

    private static byte[] AesDecrypt(byte[] data)
    {
        var key = Encoding.UTF8.GetBytes(AesKey);
        var iv  = Encoding.UTF8.GetBytes(AesIv);

        using var aes = Aes.Create();
        aes.Mode    = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key     = key;
        aes.IV      = iv;

        var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

        var decryptedBytes = decryptor.TransformFinalBlock(data, 0, data.Length);

        return decryptedBytes;
    }

    private static string Obfuscate(string data)
    {
        return BitConverter.ToString(
            MD5.HashData(Encoding.UTF8.GetBytes(data + MaiSalt))
        ).Replace("-", "").ToLower();
    }

    private static async Task<byte[]> Compress(byte[] data)
    {
        using var compressedStream = new MemoryStream();

        await using (var zLibStream = new ZLibStream(compressedStream, CompressionMode.Compress))
        {
            zLibStream.Write(data, 0, data.Length);
        }

        return compressedStream.ToArray();
    }

    private static async Task<byte[]> Decompress(byte[] data)
    {
        using var compressedStream   = new MemoryStream(data);
        using var decompressedStream = new MemoryStream();

        await using (var zLibStream = new ZLibStream(compressedStream, CompressionMode.Decompress))
        {
            await zLibStream.CopyToAsync(decompressedStream);
        }

        return decompressedStream.ToArray();
    }

    private static async Task<byte[]> MakeMaiRequest(string api, string data, int aimeId)
    {
        var entry = Obfuscate(api);

        var body = await Compress(AesEncrypt(data));

        var httpClient = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.None
        });

        var cli = new FlurlClient(httpClient);

        cli.HttpClient.DefaultRequestHeaders.Clear();

        cli.WithHeaders(new
        {
            Host             = MaiHost,
            User_Agent       = $"{entry}#{aimeId}",
            charset          = "UTF-8",
            Mai_Encoding     = "1.40",
            Content_Encoding = "deflate",
            Content_Length   = body.Length
        });

        var rep = await cli
            .Request($"https://{MaiHost}/Maimai2Servlet/{entry}")
            .PostAsync(new ByteArrayContent(body));

        var repStream = await rep.GetStreamAsync();
        var ms        = new MemoryStream();

        await repStream.CopyToAsync(ms);

        var bytes = ms.ToArray();

        return bytes;
    }

    private static async Task<List<MusicData>> GetMusicData(int aimeId)
    {
        var req = JsonConvert.SerializeObject(new
        {
            userId    = aimeId,
            maxCount  = "2147483647",
            nextIndex = "0"
        });

        var rep = await MakeMaiRequest("GetUserMusicApiMaimaiChn", req, aimeId);

        var res = Encoding.UTF8.GetString(AesDecrypt(await Decompress(rep)));

        var musicData = (dynamic)JObject.Parse(res);

        var ret = new List<MusicData>();

        foreach (var list in musicData.userMusicList)
        {
            foreach (var data in list.userMusicDetailList)
            {
                ret.Add(new MusicData(
                    (int)data.musicId,
                    (int)data.level,
                    (double)data.achievement / 10000,
                    (int)data.comboStatus,
                    (int)data.syncStatus,
                    (int)data.deluxscoreMax,
                    (int)data.playCount
                ));
            }
        }

        return ret;
    }

    private record MusicData(
        int Id,
        int LevelIndex,
        double Achievement,
        int Combo,
        int Sync,
        int DxScore,
        // ReSharper disable once NotAccessedPositionalProperty.Local
        int PlayCount);

    private record UserPreview(
        [JsonProperty("userId")] int UserId,
        [JsonProperty("userName")] string Username,
        [JsonProperty("lastLoginDate")] DateTime LastLogin
    );

    private static async Task<UserPreview> GetUserPreview(int aimeId)
    {
        var req = JsonConvert.SerializeObject(new
        {
            userId = aimeId
        });

        var rep = await MakeMaiRequest("GetUserPreviewApiMaimaiChn", req, aimeId);

        var res = Encoding.UTF8.GetString(AesDecrypt(await Decompress(rep)));

        return JsonConvert.DeserializeObject<UserPreview>(res)!;
    }

    #endregion
}
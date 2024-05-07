using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Flurl.Http;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.EntityFrameworkCore;

namespace Marisa.Plugin.Shared.Chunithm.DataFetcher;

using ChunithmSongDb =
    Util.SongDb.SongDb<ChunithmSong, Marisa.EntityFrameworkCore.Entity.Plugin.Chunithm.ChunithmGuess>;

public class AllNetBasedNetDataFetcher : DataFetcher
{
    private string? _serverUri;
    private string Host { get; }
    private string KeyChipId { get; }
    private string ServerUri => _serverUri ??= GetServerUri(KeyChipId).Result;

    public AllNetBasedNetDataFetcher(ChunithmSongDb songDb, string host, string keyChipId) : base(songDb)
    {
        Host      = host;
        KeyChipId = keyChipId;
    }

    private record MusicData(int Id, int Index, int Score, bool Fc, bool Aj, bool FullChain);

    private record RecentData(int Id, int Index, int Score);

    public override async Task<ChunithmRating> GetRating(Message message)
    {
        var aimeId = await GetAimeId(message);

        var scores = await GetScores(aimeId);

        var b30 = scores.Values.OrderByDescending(x => x.Rating).Take(30).ToList();

        var recentRep = await $"{ServerUri}ChuniServlet/GetUserRecentRatingApi".PostJsonAsync(new
        {
            userId = $"{aimeId}"
        });

        var recentData = await recentRep.GetJsonAsync();

        var res = new List<RecentData>();

        foreach (var data in recentData.userRecentRatingList)
        {
            res.Add(new RecentData(int.Parse(data.musicId), int.Parse(data.difficultId), int.Parse(data.score)));
        }

        var recent = new List<ChunithmScore>();

        foreach (var data in res)
        {
            var exist = SongDb.SongIndexer.TryGetValue(data.Id, out var song);

            if (!exist || song == null) continue;

            recent.Add(new ChunithmScore
            {
                Id          = data.Id,
                Achievement = data.Score,
                Fc          = "",
                Level       = song.Levels[data.Index],
                LevelIndex  = data.Index,
                LevelLabel  = song.LevelName[data.Index],
                Title       = song.Title,
                Constant    = (decimal)song.Constants[data.Index],
            });
        }

        var r10 = recent.OrderByDescending(x => x.Rating).Take(10).ToList();

        var userData = await (await $"{ServerUri}ChuniServlet/GetUserDataApi".PostJsonAsync(new
        {
            userId = $"{aimeId}"
        })).GetJsonAsync();

        string username = userData.userData.userName;

        return new ChunithmRating
        {
            Username = username,
            Records = new Records
            {
                B30 = b30.ToArray(),
                R10 = r10.ToArray()
            }
        };
    }

    public override async Task<Dictionary<(long Id, int LevelIdx), ChunithmScore>> GetScores(Message message)
    {
        var aimeId = await GetAimeId(message);
        return await GetScores(aimeId);
    }

    public bool Test(string accessCode)
    {
        return AccessCodeToAimeId(accessCode, KeyChipId) > 0;
    }

    private async Task<Dictionary<(long Id, int LevelIdx), ChunithmScore>> GetScores(int aimeId)
    {
        var musicRep = await $"{ServerUri}ChuniServlet/GetUserMusicApi".PostJsonAsync(new
        {
            userId    = $"{aimeId}",
            maxCount  = "2147483647",
            nextIndex = "0"
        });

        var musicData = await musicRep.GetJsonAsync();

        var res = new List<MusicData>();

        foreach (var list in musicData.userMusicList)
        {
            foreach (var data in list.userMusicDetailList)
            {
                res.Add(new MusicData(
                    int.Parse(data.musicId),
                    int.Parse(data.level),
                    int.Parse(data.scoreMax),
                    bool.Parse(data.isFullCombo),
                    bool.Parse(data.isAllJustice),
                    int.Parse(data.fullChain) != 0)
                );
            }
        }

        var ret = new Dictionary<(long Id, int LevelIndex), ChunithmScore>();

        foreach (var data in res)
        {
            var exist = SongDb.SongIndexer.TryGetValue(data.Id, out var song);

            if (!exist || song == null) continue;

            ret[(data.Id, data.Index)] = new ChunithmScore
            {
                Id          = data.Id,
                Achievement = data.Score,
                Fc          = data.Aj ? "alljustice" : data.FullChain ? "fullchain" : data.Fc ? "fullcombo" : "",
                Level       = song.Levels[data.Index],
                LevelIndex  = data.Index,
                LevelLabel  = song.LevelName[data.Index],
                Title       = song.Title,
                Constant    = (decimal)song.Constants[data.Index],
            };
        }

        return ret;
    }

    #region Helper

    private async Task<int> GetAimeId(Message message)
    {
        var qq = message.Sender!.Id;

        var at = message.MessageChain!.Messages.FirstOrDefault(m => m.Type == MessageDataType.At);
        if (at != null)
        {
            qq = (at as MessageDataAt)?.Target ?? qq;
        }

        await using var db = new BotDbContext();

        var user = db.ChunithmBinds.First(x => x.UId == qq);

        var aimeId = AccessCodeToAimeId(user!.AccessCode, KeyChipId);
        
        if (aimeId < 0) throw new InvalidDataException("该卡尚未在该服务器注册");

        return aimeId;
    }

    private async Task<string> GetServerUri(string keyChipId)
    {
        var data = $"game_id=SDHD&ver=2.20&serial={keyChipId}";

        byte[] compressedData;

        // 将字符串转换为字节数组
        var originalBytes = Encoding.UTF8.GetBytes(data);

        using (var compressedStream = new MemoryStream())
        {
            await using (var zLibStream = new ZLibStream(compressedStream, CompressionMode.Compress))
            {
                zLibStream.Write(originalBytes, 0, originalBytes.Length);
            }

            compressedData = compressedStream.ToArray();
        }

        var postData = Convert.ToBase64String(compressedData);

        var resp     = await $"http://{Host}/sys/servlet/PowerOn".PostStringAsync(postData);
        var respData = await resp.GetStringAsync();

        try
        {
            return respData.Split('&').First(x => x.StartsWith("uri")).Split('=', 2)[1];
        }
        catch (Exception)
        {
            throw new InvalidDataException("机台PowerOn请求失败，目标服务器可能宕机");
        }
    }

    private int AccessCodeToAimeId(string accessCode, string keyChipId)
    {
        var bytes    = GenerateRequestBytes(accessCode, keyChipId);
        var outBytes = new byte[48];

        var ip = Dns.GetHostAddresses(new Uri(ServerUri).Host)[0];
        using (var client = new TcpClient())
        {
            client.Connect(ip, 22345);
            using (var stream = client.GetStream())
            {
                client.Client.NoDelay = true;
                stream.Write(bytes, 0, bytes.Length);

                _ = client.GetStream().Read(outBytes, 0, outBytes.Length);

                client.GetStream().Close();
                client.Close();
            }
        }

        return DecryptResponse(outBytes);
    }

    private static byte[] GenerateRequestBytes(string accessCode, string keyChipId)
    {
        accessCode = accessCode.Replace("-", "");

        if (accessCode.Length != 20) throw new InvalidDataException("Access code length must be 20");

        var aes = Aes.Create();

        aes.Key     = AsciiToBytes(Key);
        aes.IV      = new byte[16];
        aes.Mode    = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        // keyChipId to hex
        var kc = ToHex(AsciiToBytes(keyChipId.Replace("-", ""))).PadLeft(30, '0');

        var request = HexToBytes($"3ea1ab150f003000000153444844000005{kc}{accessCode}000000000000");

        var encryptedBytes = new byte[48];
        aes.CreateEncryptor().TransformBlock(request, 0, request.Length, encryptedBytes, 0);

        return encryptedBytes;
    }

    private static int DecryptResponse(byte[] bytes)
    {
        var aes = Aes.Create();

        aes.Key     = AsciiToBytes(Key);
        aes.IV      = new byte[16];
        aes.Mode    = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        var decryptedBytes = new byte[bytes.Length];

        aes.CreateDecryptor().TransformBlock(bytes, 0, bytes.Length, decryptedBytes, 0);

        var idHex = ToHex(decryptedBytes[32..45].Reverse().ToArray());

        return int.TryParse(idHex, NumberStyles.HexNumber, null, out var id) ? id : -1;
    }

    private static string ToHex(IEnumerable<byte> bytes)
    {
        return bytes.Select(b => b.ToString("X2")).Aggregate((x, y) => x + y);
    }

    private static byte[] AsciiToBytes(string str)
    {
        var bytes = new byte[str.Length];

        for (var i = 0; i < str.Length; i++)
        {
            bytes[i] = (byte)str[i];
        }

        return bytes;
    }

    private static byte[] HexToBytes(string hex)
    {
        var bytes = new byte[hex.Length / 2];

        for (var i = 0; i < hex.Length; i += 2)
        {
            bytes[i / 2] = byte.Parse(hex.Substring(i, 2), NumberStyles.HexNumber);
        }

        return bytes;
    }

    private const string Key = "Copyright(C)SEGA";

    #endregion
}
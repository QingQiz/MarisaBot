using System.Text.RegularExpressions;
using Flurl.Http;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Entity.MessageData;
using QQBot.MiraiHttp.Util;
using QQBot.Plugin.Shared.MaiMaiDx;

namespace QQBot.Plugin.MaiMaiDx;

public partial class MaiMaiDx
{
    private List<MaiMaiSong> ListSongs(string param)
    {
        if (string.IsNullOrEmpty(param)) return _songDb.SongList;

        string[] subCommand =
        //      0       1          2        3      4     5      6       7     8    9     10      11
            { "base", "level", "charter", "bpm", "lv", "等级", "定数", "谱师", "b", "c", "artist", "a" };
        var res = param.CheckPrefix(subCommand).ToList();

        if (!res.Any()) return new List<MaiMaiSong>();

        var (prefix, index) = res.First();

        switch (index)
        {
            case 6:
            case 8:
            case 0: // base
            {
                var param1 = param.TrimStart(prefix)!.Trim();

                if (param1.Contains('-'))
                {
                    if (double.TryParse(param1.Split('-')[0], out var @base1) &&
                        double.TryParse(param1.Split('-')[1], out var @base2))
                        return _songDb.SongList.Where(s => s.Constants.Any(b => b >= base1 && b <= base2)).ToList();
                }
                else
                {
                    if (double.TryParse(param1, out var @base))
                        return _songDb.SongList.Where(s => s.Constants.Contains(@base)).ToList();
                }

                return new List<MaiMaiSong>();
            }
            case 4:
            case 5: 
            case 1: // level
            {
                var lv = param.TrimStart(prefix)!.Trim();
                return _songDb.SongList.Where(s => s.Levels.Contains(lv)).ToList();
            }
            case 7:
            case 9:
            case 2: // charter
            {
                var charter = param.TrimStart(prefix)!.Trim();
                return _songDb.SongList
                    .Where(s => s.Charters
                        .Any(c => c.Contains(charter, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }
            case 3: // bpm
            {
                var param1 = param.TrimStart(prefix)!.Trim();

                if (param1.Contains('-'))
                {
                    if (long.TryParse(param1.Split('-')[0], out var bpm1) &&
                        long.TryParse(param1.Split('-')[1], out var bpm2))
                        return _songDb.SongList.Where(s => s.Info.Bpm >= bpm1 && s.Info.Bpm <= bpm2).ToList();
                }
                else
                {
                    if (long.TryParse(param1, out var bpm)) return _songDb.SongList.Where(s => s.Info.Bpm == bpm).ToList();
                }

                return new List<MaiMaiSong>();
            }
            case 10:
            case 11: // artist
            {
                var param1 = param.TrimStart(prefix)!.Trim();
                var regex  = new Regex($@"\b{param1}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                var ls1    = _songDb.SongList.Where(s => regex.IsMatch(s.Artist)).ToList();
                return ls1.Any()
                    ? ls1.ToList()
                    : _songDb.SongList.Where(
                        s => s.Info.Artist.Contains(param1, StringComparison.OrdinalIgnoreCase)).ToList();
            }
        }

        return new List<MaiMaiSong>();
    }

    private static async Task<DxRating> GetDxRating(string? username, long? qq, bool b50 = false)
    {
        var response = await "https://www.diving-fish.com/api/maimaidxprober/query/player".PostJsonAsync(b50
            ? string.IsNullOrEmpty(username)
                ? new { qq, b50 }
                : new { username, b50 }
            : string.IsNullOrEmpty(username)
                ? new { qq }
                : new { username });
        return new DxRating(await response.GetJsonAsync(), b50);
    }

    private static async Task<MessageChain> GetB40Card(Message message, bool b50 = false)
    {
        var username = message.Command;
        var qq       = message.Sender!.Id;

        if (string.IsNullOrWhiteSpace(username))
        {
            var at = message.MessageChain!.Messages.FirstOrDefault(m => m.Type == MessageType.At);
            if (at != null)
            {
                qq = (at as AtMessage)?.Target ?? qq;
            }
        }

        MessageChain ret;
        try
        {
            ret = MessageChain.FromImageB64((await GetDxRating(username, qq, b50)).GetImage());
        }
        catch (FlurlHttpException e) when (e.StatusCode == 400)
        {
            ret = MessageChain.FromPlainText("“查无此人”");
        }
        catch (FlurlHttpException e) when (e.StatusCode == 403)
        {
            ret = MessageChain.FromPlainText("“403 forbidden”");
        }

        return ret;
    }
}
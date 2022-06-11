using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Text.RegularExpressions;
using Flurl.Http;
using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.MaiMaiDx;

public partial class MaiMaiDx
{
    #region rating

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
            var at = message.MessageChain!.Messages.FirstOrDefault(m => m.Type == MessageDataType.At);
            if (at != null)
            {
                qq = (at as MessageDataAt)?.Target ?? qq;
            }
        }

        MessageChain ret;
        try
        {
            ret = MessageChain.FromImageB64((await GetDxRating(username, qq, b50)).GetImage());
        }
        catch (FlurlHttpException e) when (e.StatusCode == 400)
        {
            ret = MessageChain.FromText("“查无此人”");
        }
        catch (FlurlHttpException e) when (e.StatusCode == 403)
        {
            ret = MessageChain.FromText("“403 forbidden”");
        }
        catch (FlurlHttpTimeoutException)
        {
            ret = MessageChain.FromText("Timeout");
        }
        catch (FlurlHttpException e)
        {
            ret = MessageChain.FromText(e.Message);
        }

        return ret;
    }

    #endregion

    #region summary

    private async Task<Dictionary<(long Id, long LevelIdx), SongScore>?> GetAllSongScores(
        Message message,
        string[]? versions = null)
    {
        var qq  = message.Sender!.Id;
        var ats = message.At().ToList();

        if (ats.Any())
        {
            qq = ats.First();
        }

        try
        {
            var response = await "https://www.diving-fish.com/api/maimaidxprober/query/plate".PostJsonAsync(new
            {
                qq, version = versions ?? MaiMaiSong.Plates
            });

            var verList = ((await response.GetJsonAsync())!.verlist as List<object>)!;

            return verList.Select(data =>
            {
                var d    = data as dynamic;
                var song = (_songDb.FindSong(d.id) as MaiMaiSong)!;
                var idx  = (int)d.level_index;

                var ach      = d.achievements;
                var constant = song.Constants[idx];

                return new SongScore(ach, constant, -1, d.fc, d.fs, d.level, idx, MaiMaiSong.LevelName[idx],
                    SongScore.Ra(ach, constant), SongScore.CalcRank(ach), song.Id, song.Title, song.Type);
            }).ToDictionary(ss => (ss.Id, ss.LevelIdx));
        }
        catch (FlurlHttpException e) when (e.StatusCode == 404)
        {
            message.Reply("NotFound");
            return null;
        }
        catch (FlurlHttpException e) when (e.StatusCode == 400)
        {
            message.Reply("400");
            return null;
        }
        catch (FlurlHttpException e) when (e.StatusCode == 403)
        {
            message.Reply("Forbidden");
            return null;
        }
        catch (FlurlHttpTimeoutException)
        {
            message.Reply("Timeout");
            return null;
        }
        catch (FlurlHttpException e)
        {
            message.Reply(e.Message);
            return null;
        }
    }

    private static Bitmap? DrawGroupedSong(
        IEnumerable<IGrouping<string, (double Constant, int LevelIdx, MaiMaiSong Song)>> groupedSong,
        IReadOnlyDictionary<(long SongId, long LevelIdx), SongScore> scores)
    {
        const int column  = 8;
        const int height  = 120;
        const int padding = 20;

        var imList = new List<Bitmap>();

        foreach (var group in groupedSong)
        {
            var key = group.Key;
            var value = group
                .Select(x => (x.LevelIdx, x.Song))
                // 先按白紫红黄绿排
                .OrderByDescending(song => song.LevelIdx)
                // 再按 ID 排
                .ThenByDescending(song => song.Song.Id)
                .ToList();

            if (value.Count == 0) continue;

            var rows = (value.Count + column - 1) / column;
            var cols = rows > 1 ? column : value.Count;

            var im = new Bitmap(cols * (height + padding) + padding, rows * (height + padding) + padding);

            using (var g = Graphics.FromImage(im))
            {
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                for (var j = 0; j < rows; j++)
                {
                    for (var i = 0; i < cols; i++)
                    {
                        var idx = j * cols + i;
                        if (idx >= value.Count) goto _break;

                        var (levelIdx, song) = value[idx];

                        var x     = (i + 1) * padding + height * i;
                        var y     = (j + 1) * padding + height * j;
                        var cover = ResourceManager.GetCover(song.Id).Resize(height, height);
                        g.DrawImage(cover, x, y);

                        // 难度指示器（小三角）
                        var path = new GraphicsPath();
                        path.AddLines(new[]
                        {
                            new Point(x, y),
                            new Point(x + 30, y),
                            new Point(x, y + 30)
                        });
                        path.CloseFigure();
                        g.FillPath(new SolidBrush(MaiMaiSong.LevelColor[levelIdx]), path);

                        g.DrawPath(Pens.White, path);

                        // 跳过没有成绩的歌
                        if (!scores.ContainsKey((song.Id, levelIdx))) continue;

                        var score = scores[(song.Id, levelIdx)];

                        var achievement = score.Achievement.ToString("F4").Split('.');

                        var font = new Font("Consolas", 24, FontStyle.Bold | FontStyle.Italic);

                        g.DrawLine(new Pen(Color.Black, 40), x, y + height - 20, x + height, y + height - 20);

                        var ach1 = (score.Achievement < 100 ? "0" : "") + achievement[0];

                        var fontColor = score.Fc switch
                        {
                            "fc"  => Brushes.LimeGreen,
                            "fcp" => Brushes.LawnGreen,
                            "ap"  => Brushes.Goldenrod,
                            "app" => Brushes.Gold,
                            _     => Brushes.White
                        };
                        // 达成率 整数部分
                        g.DrawString(ach1, font, fontColor, x, y + height - 40);

                        font = new Font("Consolas", 14, FontStyle.Bold | FontStyle.Italic);

                        // 达成率 小数部分 
                        g.DrawString("." + achievement[1], font, fontColor, x + 55, y + height - 28);

                        // rank 标志 (SSS+, SSS,...)
                        var rank = ResourceManager.GetImage($"rank_{score.Rank.ToLower()}.png");

                        g.DrawImage(rank, x + (height - rank.Width) / 2, y + (height - rank.Height - 30) / 2);
                    }
                }
            }

            _break: ;

            var bg = new Bitmap(im.Width, im.Height + 50);
            using (var g = Graphics.FromImage(bg))
            {
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                using var font = new Font("Consolas", 30, FontStyle.Bold | FontStyle.Italic);
                g.DrawString(key, font, Brushes.Black, padding, padding);
                g.DrawImage(im, 0, 50);
            }

            imList.Add(bg);
        }

        if (!imList.Any())
        {
            return null;
        }

        var res = new Bitmap(imList.Max(im => im.Width), imList.Sum(im => im.Height));
        using (var g = Graphics.FromImage(res))
        {
            g.Clear(Color.White);

            var y = 0;

            foreach (var im in imList)
            {
                g.DrawImage(im, 0, y);
                y += im.Height;
            }
        }

        return res;
    }

    private static Bitmap GetFaultTable(double tap, double bonus)
    {
        var bm = ResourceManager.GetImage("fault-table.png");

        const int hW = 133, cW = 223;
        const int hH = 75,  cH = 75;

        using (var g = Graphics.FromImage(bm))
        {
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            void DrawString(string s, Font f, int x, int y)
            {
                var m        = g.MeasureString(s, f);
                var paddingX = (cW * (x == 3 ? 2 : 1) - m.Width) / 2;
                var paddingY = (cH - m.Height) / 2;

                g.DrawString(s, f, Brushes.Black, hW + x * cW + paddingX, hH + y * cH + paddingY);
            }

            var fontS = new Font("Consolas", 30, FontStyle.Regular, GraphicsUnit.Pixel);
            var fontL = new Font("Consolas", 32, FontStyle.Regular, GraphicsUnit.Pixel);

            // perfect
            DrawString($"{0.25 * bonus:F4} / {0.5 * bonus:F4}", fontL, 3, 0);
            // great
            DrawString($"{0.2 * tap:F4}", fontL, 0, 1);
            DrawString($"{0.4 * tap:F4}", fontL, 1, 1);
            DrawString($"{0.6 * tap:F4}", fontL, 2, 1);
            DrawString($"{1.0 * tap + 0.6 * bonus:F4} / {2 * tap + 0.6 * bonus:F4} / {2.5 * tap + 0.6 * bonus:F4}",
                fontS, 3, 1);
            // good
            DrawString($"{0.5 * tap:F4}", fontL, 0, 2);
            DrawString($"{1.0 * tap:F4}", fontL, 1, 2);
            DrawString($"{1.5 * tap:F4}", fontL, 2, 2);
            DrawString($"{3.0 * tap + 0.7 * bonus:F4}", fontL, 3, 2);
            // miss
            DrawString($"{1.0 * tap:F4}", fontL, 0, 3);
            DrawString($"{2.0 * tap:F4}", fontL, 1, 3);
            DrawString($"{3.0 * tap:F4}", fontL, 2, 3);
            DrawString($"{5.0 * tap + 1.0 * bonus:F4}", fontL, 3, 3);
        }

        return bm;
    }

    #endregion

    #region Select Song

    /// <summary>
    /// 从多个筛选结果中随机选一个
    /// </summary>
    /// <param name="songs"></param>
    /// <param name="message"></param>
    private static void RandomSelectResult(List<MaiMaiSong> songs, Message message)
    {
        if (!songs.Any())
        {
            message.Reply("EMPTY");
        }
        message.Reply(MessageDataImage.FromBase64(songs.RandomTake().GetImage()));
    }

    /// <summary>
    /// 分页展示多个结果
    /// </summary>
    /// <param name="songs"></param>
    /// <param name="message"></param>
    private static void MultiPageSelectResult(IReadOnlyList<MaiMaiSong> songs, Message message)
    {
        string DisplaySong(int page)
        {
            var ret = string.Join('\n',
                songs
                    .Skip(page * SongDbConfig.PageSize)
                    .Take(SongDbConfig.PageSize)
                    .OrderBy(x => x.Id)
                    .Select(song => $"[ID:{song.Id}, Lv:{song.MaxLevel()}] -> {song.Title}"));

            if (songs.Count <= SongDbConfig.PageSize) return ret;

            var pageAll = (songs.Count + SongDbConfig.PageSize - 1) / SongDbConfig.PageSize - 1;
            ret += "\n" + $"一共有 {songs.Count} 个结果，当前页 {page}/{pageAll}";

            return ret;
        }

        switch (songs.Count)
        {
            case 0:
                message.Reply("“查无此歌”");
                break;
            case 1:
                message.Reply(new MessageDataText(songs[0].Title), MessageDataImage.FromBase64(songs[0].GetImage()));
                break;
        }

        message.Reply(DisplaySong(0));

        if (songs.Count <= SongDbConfig.PageSize)
        {
            return;
        }

        Dialog.AddHandler(message.GroupInfo?.Id, message.Sender?.Id, next =>
        {
            if (next.Command.StartsWith("p", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(next.Command[1..], out var p))
                {
                    message.Reply(DisplaySong(p));
                    return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
                }
            }
            return Task.FromResult(MarisaPluginTaskState.Canceled);
        });
    }

    private List<MaiMaiSong> SelectSongByBaseRange(string baseRange)
    {
        if (baseRange.Contains('-'))
        {
            if (double.TryParse(baseRange.Split('-')[0], out var base1) &&
                double.TryParse(baseRange.Split('-')[1], out var base2))
            {
                return _songDb.SongList.Where(s => s.Constants.Any(b => b >= base1 && b <= base2)).ToList();
            }
        }
        else
        {
            if (double.TryParse(baseRange, out var @base))
            {
                return _songDb.SongList.Where(s => s.Constants.Contains(@base)).ToList();
            }
        }

        return new List<MaiMaiSong>();
    }

    private List<MaiMaiSong> SelectSongByCharter(string charter)
    {
        return _songDb.SongList
            .Where(s => s.Charters.Any(c => c.Contains(charter, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private List<MaiMaiSong> SelectSongByLevel(string lv)
    {
        return _songDb.SongList.Where(s => s.Levels.Contains(lv)).ToList();
    }

    private List<MaiMaiSong> SelectSongByBpmRange(string bpm)
    {
        if (bpm.Contains('-'))
        {
            if (long.TryParse(bpm.Split('-')[0], out var bpm1) &&
                long.TryParse(bpm.Split('-')[1], out var bpm2))
            {
                return _songDb.SongList.Where(s => s.Info.Bpm >= bpm1 && s.Info.Bpm <= bpm2).ToList();
            }
        }
        else
        {
            if (long.TryParse(bpm, out var bpmOut))
            {
                return _songDb.SongList.Where(s => s.Info.Bpm == bpmOut).ToList();
            }
        }

        return new List<MaiMaiSong>();
    }

    private List<MaiMaiSong> SelectSongByArtist(string artist)
    {
        var regex = new Regex($@"\b{artist}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // 全字搜索
        var res = _songDb.SongList.Where(s => regex.IsMatch(s.Artist)).ToList();

        return res.Any()
            ? res.ToList()
            : _songDb.SongList.Where(
                s => s.Info.Artist.Contains(artist, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private List<MaiMaiSong> SelectSongWhenNew()
    {
        return _songDb.SongList.Where(s => s.Info.IsNew).ToList();
    }

    private List<MaiMaiSong> SelectSongWhenOld()
    {
        return _songDb.SongList.Where(s => !s.Info.IsNew).ToList();
    }

    #endregion
}
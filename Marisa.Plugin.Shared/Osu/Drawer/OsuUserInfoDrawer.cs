using System.Diagnostics.CodeAnalysis;
using Flurl.Http;
using Marisa.Plugin.Shared.Configuration;
using Marisa.Plugin.Shared.Osu.Entity.User;
using Marisa.Utils;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Marisa.Plugin.Shared.Osu.Drawer;

[SuppressMessage("ReSharper", "PossibleLossOfFraction")]
public static class OsuUserInfoDrawer
{
    private static string TempPath => ConfigurationManager.Configuration.Osu.TempPath;
    private static string ResourcePath => ConfigurationManager.Configuration.Osu.ResourcePath;

    private static async Task<Image> GetCacheOrDownload(string filename, Uri uri)
    {
        var filepath = Path.Join(TempPath, filename);
        if (File.Exists(filepath))
        {
            return await Image.LoadAsync(filepath);
        }

        var bytes = await uri.GetBytesAsync();
        await File.WriteAllBytesAsync(filepath, bytes);

        return await Image.LoadAsync(filepath);
    }

    private static async Task<Image> GetCover(this OsuUserInfo info)
    {
        var coverPath = info.Cover.CustomUrl == null
            ? $"C{info.Cover.Id}.jpg"
            : info.Cover.CustomUrl.Split('/').Last();

        return await GetCacheOrDownload(coverPath, info.Cover.Url);
    }

    private static Image GetMode(string mode)
    {
        return Image.Load(Path.Join(ResourcePath, $"mode-{mode}.png"));
    }

    private static Image GetIcon(string iconName)
    {
        return Image.Load(Path.Join(ResourcePath, $"icon-{iconName}.png"));
    }

    private static async Task<Image> GetAvatar(this OsuUserInfo info)
    {
        var avatarName = info.AvatarUrl.Query[1..];

        return await GetCacheOrDownload(avatarName, info.AvatarUrl);
    }

    public static async Task<Image> GetImage(this OsuUserInfo info)
    {
        const int imageWidth = 1000;

        var fontFamily     = SystemFonts.Get("Torus");
        var fontFamilyBold = SystemFonts.Get("Torus SemiBold");
        var fontYaHei      = SystemFonts.Get("Microsoft YaHei");

        var header = new Image<Rgba32>(imageWidth, 55);

        // 头
        header.Clear(Color.FromRgb(61, 41, 50));

        // 标题
        var font = new Font(fontYaHei, 20, FontStyle.Regular);

        var userIcon = GetIcon("user").ResizeX(40);
        header.DrawImage(userIcon, 50, (header.Height - userIcon.Height) / 2);

        const string headTitle = "玩家信息";

        var titleX = 50 + userIcon.Width + 5;
        var titleY = (header.Height - headTitle.Measure(font).Height) / 2;
        header.DrawText(headTitle, font, Color.White, titleX, titleY);

        // 游玩模式
        var mode = GetMode(info.Playmode).ResizeY(30);
        header.DrawImage(mode, header.Width - 50 - mode.Width, (header.Height - mode.Height) / 2);

        // cover image
        var cover = (await info.GetCover()).ResizeX(imageWidth);
        if (cover.Height > 250)
        {
            cover = cover.Crop(0, (cover.Height - 250) / 2, imageWidth, 250);
        }

        // name and avatar
        var nameBanner = new Image<Rgba32>(imageWidth, 85 + cover.Height);

        nameBanner.Clear(Color.FromRgb(56, 46, 50));

        //cover
        nameBanner.DrawImage(cover, 0, 0);

        // avatar
        var avatar = (await info.GetAvatar()).Resize(120, 120).RoundCorners(40);
        nameBanner.DrawImage(avatar, 50, nameBanner.Height - 10 - avatar.Height);

        var nameCard = new Image<Rgba32>(750, 55);

        font = new Font(fontFamilyBold, 24, FontStyle.Bold);

        // username
        var (_, _, nameWidth, nameHeight) = info.Username.Measure(font);
        nameCard.DrawText(info.Username, font, Color.White, 0, 0);

        // supporter
        if (info.IsSupporter)
        {
            var fontSupporter = new Font(fontFamily, 18, FontStyle.Regular);
            var supporterChar = new string('♥', info.SupportLevel);

            var supportColor = Color.FromRgb(255, 102, 171);
            nameCard.DrawText(supporterChar, fontSupporter, supportColor, nameWidth, 5);
        }

        // region flag
        var regionIcon = GetIcon(info.Region.Code.ToLower())
            .ResizeY(nameCard.Height - (int)nameHeight - 10)
            .RoundCorners(5);
        nameCard.DrawImage(regionIcon, 2, (int)(nameHeight + 5));

        // region name
        font = new Font(fontFamily, 16, FontStyle.Bold);
        var regionHeight = info.Region.Name.Measure(font).Height;
        nameCard.DrawText(info.Region.Name, font, Color.White, regionIcon.Width + 6, (nameCard.Height - nameHeight - regionHeight) / 2 + nameHeight);

        var nameCardX = 50 + avatar.Width + 20;
        var nameCardY = (nameBanner.Height - cover.Height - nameCard.Height) / 2 + cover.Height;
        nameBanner.DrawImage(nameCard, nameCardX, nameCardY);

        // level
        var levelIcon  = GetIcon("level").ResizeY(50);
        var levelIconX = nameBanner.Width - 50 - levelIcon.Width;
        var levelIconY = (nameBanner.Height - cover.Height - levelIcon.Height) / 2 + cover.Height;
        // 等级的框
        nameBanner.DrawImage(levelIcon, levelIconX, levelIconY);

        var levelFont   = new Font(fontFamilyBold, 20, FontStyle.Bold);
        var levelString = info.Statistics.Level.Current.ToString("N0");
        var (_, _, levelStringW, levelStringH) = levelString.Measure(levelFont);
        var levelStringX = levelIconX + (levelIcon.Width - levelStringW) / 2;
        var levelStringY = levelIconY + (levelIcon.Height - levelStringH) / 2;
        // 等级的值
        nameBanner.DrawText(levelString, levelFont, Color.White, levelStringX, levelStringY);

        var levelBar = new Image<Rgba32>(200, 6);
        var path     = ImageDraw.BuildCorners(levelBar.Width * info.Statistics.Level.Progress / 100, levelBar.Height, levelBar.Height / 2);

        levelBar.Mutate(i => i
            .Fill(Color.FromRgb(28, 23, 25))
            .Fill(Color.FromRgb(255, 102, 171), path)
            .RoundCorners(3)
        );

        var levelBarX = levelIconX - 15 - levelBar.Width;

        var levelBarY = levelIconY + (levelIcon.Height - levelBar.Height) / 2;

        // 等级的进度条
        nameBanner.DrawImage(levelBar, levelBarX, levelBarY);

        var progressF = new Font(fontFamily, 12, FontStyle.Regular);
        var progressS = info.Statistics.Level.Progress.ToString("N0") + '%';
        var progressM = progressS.Measure(progressF);
        var progressX = levelBarX + levelBar.Width - progressM.Width;
        var progressY = levelBarY + levelBar.Height + 5;
        nameBanner.DrawText(progressS, progressF, Color.White, progressX, progressY);

        // rank and region rank
        var rank = new Image<Rgba32>(690, 42);

        const string rankText1 = "全球排名";
        const string rankText2 = "国内/区内排名";

        var font1 = new Font(fontYaHei, 12, FontStyle.Regular);
        var font2 = new Font(fontFamily, 30, FontStyle.Bold);

        var color = Color.FromRgb(240, 219, 228);

        var text1H = rankText1.Measure(font1).Height - 8;
        rank.DrawText(rankText1, font1, Color.White, 2, 0);

        var rank1  = $"#{info.Statistics.GlobalRank:N0}";
        var text1W = Math.Max(rankText1.Measure(font1).Width, rank1.Measure(font2).Width);
        rank.DrawText(rank1, font2, color, 2, text1H);

        rank.DrawText(rankText2, font1, Color.White, text1W + 20, 0);
        rank.DrawText($"#{info.Statistics.RegionRank:N0}", font2, color, text1W + 20, text1H);

        // rank history
        var chart = new Image<Rgba32>(690, 60);

        var history = info.RankHistory.Data.ToArray();
        var min     = history.Min();
        var max     = history.Max();

        var pen = new Pen(Color.FromRgb(255, 204, 34), 2);

        if (max != min)
        {
            var x = -1.0f;
            var y = -1.0f;
            for (var i = 0; i < history.Length; i++)
            {
                var xNew = (float)i / history.Length * chart.Width;
                var yNew = (float)(history[i] - min) / (max - min) * (chart.Height - 4) + 2;

                if (x < 0 || y < 0)
                {
                    x = xNew;
                    y = yNew;
                }
                else
                {
                    var x1 = x;
                    var y1 = y;
                    chart.Mutate(im => im.DrawLines(pen, new PointF(x1, y1), new PointF(xNew, yNew)));
                    x = xNew;
                    y = yNew;
                }
            }
        }
        else
        {
            chart.Mutate(im => im
                .DrawLines(pen, new PointF(0, chart.Height / 2), new PointF(chart.Width, chart.Height / 2))
            );
        }

        // counter 奖章、pp、游戏时间、ss个数等
        var counter = new Image<Rgba32>(690, 37);

        font1 = new Font(fontFamily, 12, FontStyle.Regular);
        font2 = new Font(fontFamily, 16, FontStyle.Bold);
        var font3 = new Font(fontFamilyBold, 12, FontStyle.Bold);

        color = Color.FromRgb(240, 219, 228);

        const string text1 = "奖章";
        const string text2 = "pp";
        const string text3 = "游戏时间";

        // 奖章
        text1H = text1.Measure(font1).Height - 2;
        counter.DrawText(text1, font1, Color.White, 2, 0);
        counter.DrawText($"{info.UserAchievements.Length:N0}", font2, color, 2, text1H);

        // pp
        counter.DrawText(text2, font1, Color.White, 70, 0);
        counter.DrawText($"{info.Statistics.Pp:N0}", font2, color, 70, text1H);

        // 游戏时间
        counter.DrawText(text3, font1, Color.White, 150, 0);
        var t = TimeSpan.FromSeconds(info.Statistics.PlayTime);
        counter.DrawText($"{t.Days:N0}d {t.Hours:N0}h {t.Minutes:N0}m", font2, color, 150, text1H);

        // ss个数、s个数等
        const int iconHeight = 22;
        const int iconWidth  = 44;

        var icons = new Dictionary<string, Image>
        {
            { "a", GetIcon("rank-a").ResizeY(iconHeight) },
            { "s", GetIcon("rank-s").ResizeY(iconHeight) },
            { "sh", GetIcon("rank-s-s").ResizeY(iconHeight) },
            { "ss", GetIcon("rank-ss").ResizeY(iconHeight) },
            { "ssh", GetIcon("rank-ss-s").ResizeY(iconHeight) }
        };

        var rankX = counter.Width - iconWidth;

        foreach (var rk in new[] { "a", "s", "sh", "ss", "ssh" })
        {
            var s = info.Statistics.GradeCounts[rk].ToString("N0");
            counter.DrawImage(icons[rk], rankX, 0);
            var w = s.Measure(font1).Width;
            counter.DrawText(s, font3, Color.White, rankX + (iconWidth - w) / 2, iconHeight);

            rankX -= (int)Math.Max(w, iconWidth) + 5;
        }

        // 统计信息：总分、准确率之类的
        var summary = new Image<Rgba32>(210, 130);
        font = new Font(fontFamily, 12, FontStyle.Regular);

        var textHeader = new[]
        {
            "Ranked 谱面总分", "准确率", "游戏次数", "总分", "总命中次数", "最大连击", "回放被观看次数"
        };

        var st = info.Statistics;
        var value = new[]
        {
            st.RankedScore.ToString("N0"), st.HitAccuracy.ToString("F2") + "%", st.PlayCount.ToString("N0"),
            st.TotalScore.ToString("N0"), st.TotalHits.ToString("N0"), st.MaximumCombo.ToString("N0"),
            st.ReplaysWatchedByOthers.ToString("N0")
        };


        var textHeight = (float)summary.Height / textHeader.Length;
        var padding    = (int)(textHeight - textHeader[0].Measure(font).Height) / 2;

        var headerWidth = 0f;

        for (var i = 0; i < textHeader.Length; i++)
        {
            headerWidth = Math.Max(headerWidth, textHeader[i].Measure(font).Width);
            summary.DrawText(textHeader[i], font, Color.White, 0, padding + textHeight * i);
        }

        headerWidth += 10;

        for (var i = 0; i < value.Length; i++)
        {
            summary.DrawText(value[i], font, Color.White, headerWidth, padding + textHeight * i);
        }

        // 把上面几个小卡片拼接起来
        var userDetail = new Image<Rgba32>(imageWidth, 190);

        userDetail.Clear(Color.FromRgb(42, 34, 38));

        padding = (userDetail.Height - (rank.Height + chart.Height + counter.Height) - 20) / 2;

        var pen1 = new Pen(Color.FromRgb(28, 23, 25), 2);
        userDetail.Mutate(i => i
            .DrawImage(rank, 50, 10)
            .DrawImage(chart, 50, 10 + rank.Height + padding)
            .DrawImage(counter, 50, 10 + rank.Height + chart.Height + padding + padding)
            .DrawLines(pen1, new PointF(50 + chart.Width + 15, 10), new PointF(50 + chart.Width + 14, userDetail.Height - 10))
            .DrawImage(summary, 50 + chart.Width + 30, (userDetail.Height - 20 - summary.Height) / 2 + 10)
        );

        // 把它们拼起来
        var bg = new Image<Rgba32>(imageWidth, header.Height + nameBanner.Height + userDetail.Height);

        var pen2 = new Pen(Color.FromRgb(37, 30, 34), 1);
        bg.Mutate(i => i
            .DrawImage(header, 0, 0)
            .DrawImage(nameBanner, 0, header.Height)
            .DrawLines(pen2, new PointF(0, header.Height + nameBanner.Height), new PointF(imageWidth, header.Height + nameBanner.Height))
            .DrawImage(userDetail, 0, header.Height + nameBanner.Height)
        );

        return bg.RoundCorners(20);
    }
}
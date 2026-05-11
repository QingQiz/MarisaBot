using System.Diagnostics.CodeAnalysis;
using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.Util;

namespace Marisa.Plugin.MaiMaiDx;

[SuppressMessage("ReSharper", "UnusedMember.Local")]
public partial class MaiMaiDx
{
    /// <summary>
    ///     触发器：消息以"完成表"结尾即接管。完整解析放到 handler 里做（避免 trigger 阶段
    ///     做重活，因为 charters 列表是动态从 SongDb 提取的）。
    /// </summary>
    public static MarisaPluginTrigger.PluginTrigger PlateProgressTrigger => (message, _) =>
        message.Command.EndsWith(PlateData.CommandSuffix);

    [MarisaPluginDoc(
        "查询版本/谱师/类别的完成表",
        "格式：`<选择条件><阈值>完成表`。"
      + "选择条件可以是版本代字（真/熊/华/鏡/...，可加'代'后缀如'真代'），谱师名（如'翠楼屋'），"
      + "或类别名/别名（如'术力口'/'东方'）。"
      + "阈值支持 SSS+/SSS/SS+/.../A/...（Achievement）、FC/FC+/AP/AP+（Fc）、FS/FS+/FDX/FDX+（Fs），"
      + "也支持中文别名：将=SSS，大将=SSS+，理论值=AP+，舞舞=FDX，极=FC，神=AP。"
      + "例：`mai 真大将完成表`、`mai 翠楼屋将完成表`、`mai 术力口神完成表`。"
    )]
    [MarisaPluginTrigger(typeof(MaiMaiDx), nameof(PlateProgressTrigger))]
    private async Task<MarisaPluginTaskState> PlateProgress(Message message)
    {
        var raw = message.Command.ToString();

        var charters = SongDb.SongList
            .SelectMany(s => s.Charters)
            .Where(c => !string.IsNullOrWhiteSpace(c) && c != "-" && c != "N/A")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!PlateData.TryParse(raw, charters, out var query, out var error))
        {
            // NotPlateCommand 不该到这里（trigger 已挡住），保险起见兜底
            if (error!.Kind == PlateData.ErrorKind.NotPlateCommand)
            {
                return MarisaPluginTaskState.NoResponse;
            }
            message.Reply(FormatError(error));
            return MarisaPluginTaskState.CompletedTask;
        }

        var pairs = SelectChartsForQuery(query!);

        if (pairs.Count == 0)
        {
            message.Reply($"没有找到 {query!.Selector.Display} 对应的歌曲");
            return MarisaPluginTaskState.CompletedTask;
        }

        var fetcher = GetDataFetcher(message);
        var scores  = await fetcher.GetScores(message);

        // 标题原样使用用户输入的命令文本（含"完成表"），如用户所要求
        var titleText = raw.Trim();

        var im = await Task.Run(() => MaiMaiDraw.DrawPlateProgress(query!, pairs, scores, titleText));
        message.Reply(MessageDataImage.FromBase64(im.ToB64()));

        return MarisaPluginTaskState.CompletedTask;
    }

    private static string FormatError(PlateData.ParseError error) => error.Kind switch
    {
        PlateData.ErrorKind.UnsupportedPlate => $"不支持该版本：{error.Detail}",
        PlateData.ErrorKind.UnknownThreshold => $"无法识别阈值，请检查：{error.Detail}",
        PlateData.ErrorKind.UnknownSelector  => $"无法识别版本/谱师/类别：{error.Detail}",
        PlateData.ErrorKind.EmptyQuery       => "请在'完成表'前指定 <版本/谱师/类别> + <阈值>",
        _                                    => "命令格式错误",
    };

    /// <summary>
    ///     按 query 的 selector 类型筛出对应的 (constant, levelIdx, song) 三元组。
    ///     完成表默认只看 MASTER (i=3)；用户在命令里加难度别名（如"红谱"/"EXPERT"）则筛对应难度。
    ///     Re:MASTER 永远不参与（DifficultyAliasMap 里就没收）。
    /// </summary>
    private List<(double Constant, int LevelIdx, MaiMaiSong Song)> SelectChartsForQuery(PlateData.Query query)
    {
        var levelIdx = query.LevelIdx;

        var allCharts = SongDb.SongList
            .SelectMany(song => song.Constants
                .Select((constant, i) => (constant, i, song)))
            .Where(t => t.i == levelIdx);

        return query.Selector switch
        {
            PlateData.Selector.Plate p => allCharts
                .Where(t => p.Versions.Any(v => string.Equals(v, t.song.Version, StringComparison.OrdinalIgnoreCase)))
                .Select(t => (t.constant, t.i, t.song))
                .ToList(),

            // substring 匹配：兼容 "サファ太 vs 翠楼屋" 这种合作谱师名义。
            PlateData.Selector.Charter c => allCharts
                .Where(t => t.i < t.song.Charters.Count
                         && t.song.Charters[t.i].Contains(c.Name, StringComparison.OrdinalIgnoreCase))
                .Select(t => (t.constant, t.i, t.song))
                .ToList(),

            PlateData.Selector.Genre g => allCharts
                .Where(t => string.Equals(t.song.Info.Genre, g.FullName, StringComparison.Ordinal))
                .Select(t => (t.constant, t.i, t.song))
                .ToList(),

            _ => [],
        };
    }
}

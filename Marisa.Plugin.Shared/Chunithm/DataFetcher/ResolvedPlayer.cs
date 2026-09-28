namespace Marisa.Plugin.Shared.Chunithm.DataFetcher;

/// <summary>
///     一次查询的目标：查谁、要不要按账号名查、是不是查询者本人，以及按绑定选定的查分器。
///     由插件层从消息解析一次后传给 fetcher（插件里是 ResolvePlayer），fetcher 不再自己从消息里反推目标，
///     于是也不需要 allowUsername / qqOnly 这类开关和「把 Command 清空」的改写。
/// </summary>
/// <param name="Qq">目标的 QQ；按账号名查询时是发起者的 QQ（只用于绑定与兜底显示）。</param>
/// <param name="Username">查分器账号名；账号名查询只会落到水鱼（Louis 也支持但未启用），其余查分器收到它应忽略。</param>
/// <param name="IsSelf">目标就是发起者本人——只有本人才能用本人 OAuth 票据读完整成绩。</param>
/// <param name="Fetcher">该目标的取数器；调用方用它发起查询，fetcher 自身不需要读这个字段。</param>
public sealed record ResolvedPlayer(long Qq, string? Username, bool IsSelf, DataFetcher Fetcher);

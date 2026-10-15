namespace Marisa.Plugin.Shared.Versus;

/// <summary>每次命令先准备独立的对战上下文；触发判断仅解析语法，不加载资料。</summary>
public sealed class PreparingVersusBattle(Func<string, bool> matchScope, Func<Task<VersusBattle>> prepare) : VersusBattle
{
    public override bool CanParseScope(string query) => matchScope(query);

    public override async Task<MarisaPluginTaskState> Single(Message message) =>
        await (await prepare()).Single(message);

    public override async Task<MarisaPluginTaskState> Random(Message message) =>
        await (await prepare()).Random(message);

    public override async Task<MarisaPluginTaskState> Batch(Message message) =>
        await (await prepare()).Batch(message);

    public override async Task<MarisaPluginTaskState> SingleRoom(Message message, long botQq) =>
        await (await prepare()).SingleRoom(message, botQq);

    public override async Task<MarisaPluginTaskState> RandomRoom(Message message, long botQq) =>
        await (await prepare()).RandomRoom(message, botQq);

    public override async Task<MarisaPluginTaskState> BatchRoom(Message message, long botQq) =>
        await (await prepare()).BatchRoom(message, botQq);
}

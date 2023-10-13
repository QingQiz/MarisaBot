namespace Marisa.Plugin;

[MarisaPluginNoDoc]
[MarisaPluginCommand("")]
[MarisaPlugin(PluginPriority.WordCloud)]
public class WordCloud : MarisaPluginBase
{
    private readonly Dictionary<long, List<string>> _cache = new();

    private static string PreparePath(long groupId)
    {
        var tempPath   = ConfigurationManager.Configuration.WordCloud.TempPath;
        var parentPath = Path.Combine(tempPath, groupId.ToString());
        var filePath   = Path.Combine(parentPath, $"{DateTime.Now:yyyy-MM-dd}.txt");

        if (!Directory.Exists(parentPath))
        {
            Directory.CreateDirectory(parentPath);
        }

        return filePath;
    }

    private void SetCache(long groupId, string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return;

        lock (_cache)
        {
            if (!_cache.TryGetValue(groupId, out var value))
            {
                value = new List<string>();
                _cache.Add(groupId, value);
            }

            value.Add(word);

            if (value.Count <= 20) return;

            File.AppendAllLines(PreparePath(groupId), value.Select(Uri.EscapeDataString));
            value.Clear();
        }
    }

    [MarisaPluginTrigger(nameof(MarisaPluginTrigger.AlwaysTrueTrigger), MessageType.GroupMessage)]
    private MarisaPluginTaskState Collect(Message message)
    {
        var text = message.Command;

        Task.Run(() => { SetCache(message.GroupInfo!.Id, text); });

        return MarisaPluginTaskState.NoResponse;
    }

    [MarisaPluginCommand(MessageType.GroupMessage, false, ":wc")]
    private static MarisaPluginTaskState Generate(Message message)
    {
        return MarisaPluginTaskState.CompletedTask;
    }
}
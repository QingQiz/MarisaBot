using System.Text.Json;
using Marisa.Configuration;
using NLog;

namespace Marisa.BotDriver.Plugin;

public static class ExceptionDump
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static string? Save(Exception exception, string? relatedMessage = null, string? source = null)
    {
        try
        {
            var directory = Path.Join(ConfigurationManager.Configuration.TempPath, "exceptions");
            Directory.CreateDirectory(directory);

            var timestamp = DateTimeOffset.UtcNow;
            var fileName = $"{timestamp:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.json";
            var filePath = Path.Join(directory, fileName);

            var payload = new ExceptionDumpPayload(
                timestamp,
                source ?? "unknown",
                exception.GetType().FullName ?? exception.GetType().Name,
                exception.Message,
                exception.ToString(),
                relatedMessage
            );

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(filePath, json);
            return filePath;
        }
        catch (Exception dumpException)
        {
            Logger.Warn(dumpException, "Failed to persist exception dump");
            return null;
        }
    }

    private sealed record ExceptionDumpPayload(
        DateTimeOffset TimestampUtc,
        string Source,
        string ExceptionType,
        string Message,
        string Detail,
        string? RelatedMessage
    );
}

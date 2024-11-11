namespace Marisa.Plugin.Shared.Util;

public static class DateTimeExt
{
    public static string TimeAgo(this DateTimeOffset date)
    {
        var now  = DateTime.Now;
        var diff = now - date;

        switch (diff.TotalDays)
        {
            case > 365:
                return $"{diff.TotalDays / 365:N0} 年前";
            case > 30:
                return $"{diff.TotalDays / 30:N0} 个月前";
            case > 1:
                return $"{diff.TotalDays:N0} 天前";
        }

        if (diff.TotalHours > 1)
        {
            return $"{diff.TotalHours:N0} 小时前";
        }

        if (diff.TotalMinutes > 1)
        {
            return $"{diff.TotalMinutes:N0} 分钟前";
        }

        if (diff.TotalSeconds > 1)
        {
            return $"{diff.TotalSeconds:N0} 秒前";
        }

        return "刚刚";
    }
}
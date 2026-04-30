namespace Marisa.Plugin.Shared.Util;

public static class VersionOrderHelper
{
    public static string[] BuildVersionList<TSong>(IEnumerable<TSong> songs, Func<TSong, string> versionSelector, Func<TSong, long> idSelector)
    {
        return [.. songs
            .Where(song => !string.IsNullOrWhiteSpace(versionSelector(song)))
            .GroupBy(versionSelector, StringComparer.OrdinalIgnoreCase)
            .Select(group => new VersionGroup(group.Key, [.. group.Select(idSelector).OrderBy(id => id)]))
            .OrderBy(group => group, VersionGroupComparer.Instance)
            .Select(group => group.Name)];
    }

    private sealed record VersionGroup(string Name, long[] OrderedIds)
    {
        public long MajorityId => OrderedIds[(OrderedIds.Length - 1) / 2];

        public double AverageId => OrderedIds.Average(id => (double)id);

        public long MinId => OrderedIds[0];
    }

    private sealed class VersionGroupComparer : IComparer<VersionGroup>
    {
        public static readonly VersionGroupComparer Instance = new();

        public int Compare(VersionGroup? x, VersionGroup? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;

            var result = x.MajorityId.CompareTo(y.MajorityId);
            if (result != 0) return result;

            result = x.AverageId.CompareTo(y.AverageId);
            if (result != 0) return result;

            result = x.MinId.CompareTo(y.MinId);
            if (result != 0) return result;

            return StringComparer.OrdinalIgnoreCase.Compare(x.Name, y.Name);
        }
    }
}
namespace QQBot.Plugin.Shared.Util
{
    public static class ListExt
    {
        private static readonly Random Rand = new();

        public static T RandomTake<T>(this List<T> list)
        {
            return list[Rand.Next(list.Count)];
        }
    }
}
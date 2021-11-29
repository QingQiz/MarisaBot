using System;
using System.Collections.Generic;

namespace QQBOT.Core.Util
{
    public static class ListExt
    {
        public static T RandomTake<T>(this List<T> list)
        {
            var cnt = list.Count;
            return list[new Random().Next(cnt)];
        }
    }
}
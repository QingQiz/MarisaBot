﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace QQBOT.Core.Util
{
    public static class StringExt
    {
        public static string TrimStart(this string msg, IEnumerable<string> prefixes)
        {
            msg = msg.Trim();

            return (from prefix in prefixes
                where msg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                select msg[prefix.Length..].Trim()).FirstOrDefault();
        }

        public static string TrimStart(this string msg, string prefix)
        {
            msg = msg.Trim();

            return msg.StartsWith(prefix) ? msg[prefix.Length..] : null;
        }

        public static IEnumerable<(string Prefix, int Index)> CheckPrefix(this string msg, IEnumerable<string> prefixes)
        {
            return prefixes.Select((p, i) => (p, i))
                .Where(x => msg.StartsWith(x.p, StringComparison.OrdinalIgnoreCase));
        }
    }
}
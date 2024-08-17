using System.Reflection;

namespace Marisa.BotDriver.Extension;

public static class ReflectionExt
{
    private const BindingFlags BindingFlags =
        System.Reflection.BindingFlags.Default
      | System.Reflection.BindingFlags.NonPublic
      | System.Reflection.BindingFlags.Instance
      | System.Reflection.BindingFlags.Static
      | System.Reflection.BindingFlags.Public;

    public static IEnumerable<MethodInfo> GetAllMethods(this Type t, BindingFlags flags = BindingFlags)
    {
        foreach (var i in t.GetMethods(flags))
        {
            yield return i;
        }

        foreach (var i in t.GetInterfaces())
        {
            foreach (var j in i.GetMethods(flags))
            {
                yield return j;
            }
        }
    }
}
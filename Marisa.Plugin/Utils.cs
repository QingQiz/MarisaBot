using System.Reflection;

namespace Marisa.Plugin;

public static class Utils
{
    public static Assembly Assembly()
    {
        return AppDomain.CurrentDomain.GetAssemblies().First(x => x.GetName().Name!.Equals("Marisa.Plugin"));
    }
}
namespace QQBOT.Core.Attribute
{
    public class MiraiPluginAttribute : System.Attribute
    {
        public readonly string CommandPrefix;

        public MiraiPluginAttribute(string commandPrefix = null)
        {
            CommandPrefix = commandPrefix;
        }
    }

    public class MiraiPluginDisabledAttribute : System.Attribute
    {
        
    }
}
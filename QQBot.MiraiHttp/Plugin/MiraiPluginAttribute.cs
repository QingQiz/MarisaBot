namespace QQBot.MiraiHttp.Plugin
{
    public class MiraiPluginAttribute : Attribute
    {
        public readonly long Priority;

        /// <param name="priority">插件的优先级。高则先处理消息</param>
        public MiraiPluginAttribute(long priority = 0)
        {
            Priority = priority;
        }
    }

    public class MiraiPluginDisabledAttribute : Attribute
    {
    }
}
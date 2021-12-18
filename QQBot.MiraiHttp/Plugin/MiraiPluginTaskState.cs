namespace QQBot.MiraiHttp
{
    public enum MiraiPluginTaskState
    {
        /// <summary>
        /// 插件未处理
        /// </summary>
        NoResponse,

        /// <summary>
        /// 插件处理完成
        /// </summary>
        CompletedTask,

        /// <summary>
        /// 插件处理了一部分，但仍需后续处理
        /// </summary>
        ToBeContinued
    }
}
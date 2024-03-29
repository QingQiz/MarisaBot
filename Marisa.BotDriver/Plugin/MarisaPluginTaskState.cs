﻿namespace Marisa.BotDriver.Plugin;

public enum MarisaPluginTaskState
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
    ToBeContinued,
        
    /// <summary>
    /// 插件自闭了
    /// </summary>
    Canceled
}
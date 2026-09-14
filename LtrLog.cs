using System;

namespace LuckyTrinket
{
    /// <summary>轻量日志包装：Debug 级别受配置开关控制。</summary>
    internal static class LtrLog
    {
        internal static void Info(string message)
        {
            LuckyTrinketPlugin.Log?.LogInfo(message);
        }

        internal static void Warn(string message)
        {
            LuckyTrinketPlugin.Log?.LogWarning(message);
        }

        internal static void Error(string message, Exception exception = null)
        {
            if (exception == null)
            {
                LuckyTrinketPlugin.Log?.LogError(message);
            }
            else
            {
                LuckyTrinketPlugin.Log?.LogError($"{message} {exception}");
            }
        }

        internal static void Debug(string message)
        {
            if (LuckyTrinketPlugin.DebugLog != null && LuckyTrinketPlugin.DebugLog.Value)
            {
                LuckyTrinketPlugin.Log?.LogInfo("[debug] " + message);
            }
        }
    }
}

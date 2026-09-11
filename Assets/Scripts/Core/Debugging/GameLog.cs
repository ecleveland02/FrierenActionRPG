using UnityEngine;
using Conditional = System.Diagnostics.ConditionalAttribute;

namespace Frieren.Core.Debugging
{
    /// <summary>
    /// Channel-filtered logging that compiles out of release builds.
    /// </summary>
    /// <remarks>
    /// <see cref="Info"/> and <see cref="Warn"/> carry <c>[Conditional]</c> on UNITY_EDITOR and
    /// DEVELOPMENT_BUILD, so in a release player the calls - and the string concatenation at every
    /// call site - are removed by the compiler rather than merely ignored at runtime.
    /// <see cref="Error"/> is deliberately not conditional: a shipped build should still record
    /// something that went genuinely wrong.
    /// </remarks>
    public static class GameLog
    {
        private const string EditorDefine = "UNITY_EDITOR";
        private const string DevelopmentDefine = "DEVELOPMENT_BUILD";

        /// <summary>Channels that produce output. Set from <see cref="LogSettings"/> at boot.</summary>
        public static LogChannel EnabledChannels { get; set; } = LogChannel.All;

        [Conditional(EditorDefine), Conditional(DevelopmentDefine)]
        public static void Info(LogChannel channel, string message, Object context = null)
        {
            if (IsEnabled(channel))
            {
                Debug.Log(Format(channel, message), context);
            }
        }

        [Conditional(EditorDefine), Conditional(DevelopmentDefine)]
        public static void Warn(LogChannel channel, string message, Object context = null)
        {
            if (IsEnabled(channel))
            {
                Debug.LogWarning(Format(channel, message), context);
            }
        }

        /// <summary>Errors bypass channel filtering - a silenced channel should not hide a fault.</summary>
        public static void Error(LogChannel channel, string message, Object context = null)
        {
            Debug.LogError(Format(channel, message), context);
        }

        public static bool IsEnabled(LogChannel channel) => (EnabledChannels & channel) != 0;

        private static string Format(LogChannel channel, string message) => $"[{channel}] {message}";
    }
}

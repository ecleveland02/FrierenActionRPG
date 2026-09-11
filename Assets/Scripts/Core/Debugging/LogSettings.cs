using Frieren.Data;
using UnityEngine;

namespace Frieren.Core.Debugging
{
    /// <summary>
    /// Authored logging configuration applied by the bootstrapper.
    /// </summary>
    [CreateAssetMenu(menuName = "Frieren/Core/Log Settings", fileName = "LogSettings", order = 10)]
    public sealed class LogSettings : IdentifiableScriptableObject
    {
        [SerializeField] private LogChannel enabledChannels = LogChannel.All;

        [SerializeField]
        [Tooltip("Show the on-screen debug overlay when the game starts. Toggle at runtime with F1.")]
        private bool showDebugOverlayOnStart;

        public LogChannel EnabledChannels => enabledChannels;

        public bool ShowDebugOverlayOnStart => showDebugOverlayOnStart;

        public void Apply()
        {
            GameLog.EnabledChannels = enabledChannels;
        }
    }
}

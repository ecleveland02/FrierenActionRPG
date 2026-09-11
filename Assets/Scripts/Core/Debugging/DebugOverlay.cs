using Frieren.Core.Input;
using Frieren.Core.Scenes;
using Frieren.Core.Services;
using Frieren.Core.StateMachine;
using Frieren.Save;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Frieren.Core.Debugging
{
    /// <summary>
    /// On-screen diagnostics panel: frame timing, game state, loaded scenes and save shortcuts.
    /// </summary>
    /// <remarks>
    /// Uses IMGUI on purpose. It needs no scene setup, no canvas and no prefab, so it works in any
    /// scene including bare test scenes, and it will not collide with the real UI built later.
    /// The whole body compiles out of release builds.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class DebugOverlay : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const float SampleInterval = 0.25f;

        private static readonly Rect PanelRect = new Rect(10f, 10f, 420f, 235f);

        private GUIStyle panelStyle;
        private float accumulatedTime;
        private int accumulatedFrames;
        private float framesPerSecond;
        private float millisecondsPerFrame;
        private string lastAction = "-";

        public bool IsVisible { get; private set; }

        public void SetVisible(bool visible) => IsVisible = visible;

        private void Update()
        {
            SampleFrameRate();
            ReadShortcuts();
        }

        private void SampleFrameRate()
        {
            accumulatedTime += Time.unscaledDeltaTime;
            accumulatedFrames++;

            if (accumulatedTime < SampleInterval)
            {
                return;
            }

            framesPerSecond = accumulatedFrames / accumulatedTime;
            millisecondsPerFrame = accumulatedTime / accumulatedFrames * 1000f;
            accumulatedTime = 0f;
            accumulatedFrames = 0;
        }

        private void ReadShortcuts()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return;
            }

            if (keyboard.f1Key.wasPressedThisFrame)
            {
                IsVisible = !IsVisible;
            }

            if (keyboard.f5Key.wasPressedThisFrame && ServiceLocator.TryGet(out SaveService saveOnWrite))
            {
                lastAction = saveOnWrite.Save() ? "Quick saved" : "Quick save FAILED";
                GameLog.Info(LogChannel.Save, lastAction);
            }

            if (keyboard.f9Key.wasPressedThisFrame && ServiceLocator.TryGet(out SaveService saveOnRead))
            {
                lastAction = saveOnRead.Load() ? "Quick loaded" : "Quick load FAILED (no save?)";
                GameLog.Info(LogChannel.Save, lastAction);
            }
        }

        private void OnGUI()
        {
            if (!IsVisible)
            {
                return;
            }

            panelStyle ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, padding = new RectOffset(10, 10, 10, 10) };

            GUILayout.BeginArea(PanelRect, panelStyle);
            GUILayout.Label($"FPS {framesPerSecond:0.0}   ({millisecondsPerFrame:0.0} ms)");
            GUILayout.Label($"State: {DescribeGameState()}   timeScale {Time.timeScale:0.##}");
            GUILayout.Label($"Scenes: {DescribeScenes()}");
            GUILayout.Label($"Save: {DescribeSave()}");
            GUILayout.Label($"Input: {DescribeInput()}");
            GUILayout.Label($"Last action: {lastAction}");
            GUILayout.Space(6f);
            GUILayout.Label("F1 overlay   F5 quick save   F9 quick load");
            GUILayout.EndArea();
        }

        private static string DescribeGameState()
        {
            return ServiceLocator.TryGet(out GameStateMachine stateMachine)
                ? stateMachine.Current.ToString()
                : "no state machine";
        }

        private static string DescribeScenes()
        {
            if (!ServiceLocator.TryGet(out SceneLoader loader))
            {
                return "no loader";
            }

            if (loader.IsBusy)
            {
                return "loading...";
            }

            return loader.ActiveGameplayScene != null ? loader.ActiveGameplayScene.ToString() : "none active";
        }

        /// <summary>
        /// Live action values. This is the line that separates "the action never fired" from
        /// "input arrived and something downstream ignored it", which are indistinguishable from
        /// the outside and were costing a round trip each time to tell apart.
        /// </summary>
        private static string DescribeInput()
        {
            if (!ServiceLocator.TryGet(out InputReader input))
            {
                return "no input reader";
            }

            if (!input.IsInitialized)
            {
                return "reader not initialised";
            }

            return $"move {input.MoveInput.x:0.00},{input.MoveInput.y:0.00}   " +
                   $"look {input.LookInput.x:0.0},{input.LookInput.y:0.0}   " +
                   $"sprint {(input.SprintHeld ? 1 : 0)}";
        }

        private static string DescribeSave()
        {
            if (!ServiceLocator.TryGet(out SaveService saveService))
            {
                return "no save service";
            }

            int registered = saveService.RegisteredIds.Count;

            return saveService.Current == null
                ? $"no file in memory ({registered} saveables)"
                : $"v{saveService.Current.Version}, saved {saveService.Current.SavedUtc} ({registered} saveables)";
        }
#else
        public bool IsVisible => false;

        public void SetVisible(bool visible)
        {
        }
#endif
    }
}

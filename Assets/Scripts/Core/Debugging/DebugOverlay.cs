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

        private static readonly Rect PanelRect = new Rect(10f, 10f, 340f, 210f);

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
            GUILayout.Label($"State: {DescribeGameState()}");
            GUILayout.Label($"Scenes: {DescribeScenes()}");
            GUILayout.Label($"Save: {DescribeSave()}");
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

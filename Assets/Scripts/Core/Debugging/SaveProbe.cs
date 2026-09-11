using System;
using Frieren.Core.Services;
using Frieren.Save;
using Frieren.Save.Serialization;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Frieren.Core.Debugging
{
    /// <summary>
    /// A trivial saveable used to prove the save pipeline end to end from the test scene.
    /// </summary>
    /// <remarks>
    /// Press F6 to change its counter, F5 to save, F6 again, then F9 to load: the counter should
    /// snap back to the saved value. It is also the reference implementation of
    /// <see cref="ISaveable"/> - a real system's state class looks exactly like
    /// <see cref="ProbeState"/>: plain serialisable fields, no object references.
    ///
    /// Delete this component once real saveable systems exist.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SaveProbe : MonoBehaviour, ISaveable
    {
        [Serializable]
        private sealed class ProbeState
        {
            public int counter;
            public string lastTouchedUtc;
        }

        [SerializeField] private string saveId = "debug.save_probe";

        private SaveService saveService;
        private ProbeState state = new ProbeState();

        public string SaveId => saveId;

        public int Counter => state.counter;

        private void Start()
        {
            if (!ServiceLocator.TryGet(out saveService))
            {
                GameLog.Warn(LogChannel.Save, "SaveProbe found no SaveService; is the Boot scene loaded?", this);
                return;
            }

            saveService.Register(this);
        }

        private void OnDestroy()
        {
            saveService?.Unregister(this);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard.f6Key.wasPressedThisFrame)
            {
                state.counter++;
                state.lastTouchedUtc = DateTime.UtcNow.ToString("o");
                GameLog.Info(LogChannel.Save, $"SaveProbe counter is now {state.counter}.", this);
            }
        }

        public SaveEntry Capture() => SaveEntry.Create(saveId, state);

        public void Restore(SaveEntry entry)
        {
            if (entry.TryRead(out ProbeState restored))
            {
                state = restored;
                GameLog.Info(LogChannel.Save, $"SaveProbe restored counter {state.counter}.", this);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnGUI()
        {
            GUI.Label(new Rect(10f, 230f, 340f, 22f), $"SaveProbe counter: {state.counter}   (F6 to change)");
        }
#endif
    }
}

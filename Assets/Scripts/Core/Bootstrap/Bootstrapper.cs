using Frieren.Core.Debugging;
using Frieren.Core.Input;
using Frieren.Core.Scenes;
using Frieren.Core.Services;
using Frieren.Core.StateMachine;
using Frieren.Core.Timing;
using Frieren.Save;
using Frieren.Save.Storage;
using UnityEngine;

namespace Frieren.Core.Bootstrap
{
    /// <summary>
    /// The game's composition root. Lives in the Boot scene and is the only place services are
    /// constructed and wired together.
    /// </summary>
    /// <remarks>
    /// Everything downstream resolves its dependencies from <see cref="ServiceLocator"/>, which
    /// means there is exactly one file to read to understand what exists at runtime and in what
    /// order it comes up. New systems are added here, not by sprinkling more singletons.
    /// </remarks>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class Bootstrapper : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField]
        [Tooltip("Scene loaded after boot. Leave empty to boot into whatever scene is already open.")]
        private GameSceneDefinition firstScene;

        [SerializeField] private SceneCatalog sceneCatalog;

        [Header("Systems")]
        [SerializeField] private InputReader inputReader;

        [SerializeField] private LogSettings logSettings;

        [Header("Save")]
        [SerializeField]
        [Tooltip("Keep saves in memory only. Useful while iterating so test data never hits disk.")]
        private bool useInMemorySaves;

        private GameStateMachine stateMachine;
        private readonly TimeScaleService timeScale = new TimeScaleService();
        private DebugOverlay debugOverlay;
        private bool pauseTogglePending;

        public static bool IsInitialized { get; private set; }

        /// <summary>The live instance, or <c>null</c> before boot. Prefer <see cref="ServiceLocator"/>.</summary>
        public static Bootstrapper Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Reached by entering play mode from a gameplay scene that already pulled Boot in.
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            ServiceLocator.Clear();
            ConfigureLogging();
            CreateServices();

            IsInitialized = true;
            GameLog.Info(LogChannel.Core, "Bootstrap complete.", this);
        }

        private void Start()
        {
            if (SceneBootstrapGuard.BootedFromAnotherScene)
            {
                // A gameplay scene is already open because play mode was entered from it.
                // Loading firstScene here would throw away the scene being tested.
                GameLog.Info(LogChannel.Core, "Booted from an existing scene; skipping first-scene load.", this);
                stateMachine.ChangeTo(GameStateId.Playing);
                return;
            }

            if (firstScene == null)
            {
                GameLog.Warn(LogChannel.Core, "No first scene assigned; staying in the Boot scene.", this);
                stateMachine.ChangeTo(GameStateId.Playing);
                return;
            }

            LoadFirstScene();
        }

        private void Update()
        {
            // The dip expires on real time, so it must be ticked before anything reads deltaTime,
            // and it must keep ticking while paused - a zero scale still runs Update.
            timeScale.Tick();
            ApplyPendingPauseToggle();
            stateMachine?.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            if (inputReader != null)
            {
                inputReader.PausePerformed -= RequestPauseToggle;
                inputReader.Dispose();
            }

            ServiceLocator.Clear();
            Instance = null;
            IsInitialized = false;
        }

        private void ConfigureLogging()
        {
            if (logSettings != null)
            {
                logSettings.Apply();
            }

            if (!TryGetComponent(out debugOverlay))
            {
                debugOverlay = gameObject.AddComponent<DebugOverlay>();
            }

            debugOverlay.SetVisible(logSettings != null && logSettings.ShowDebugOverlayOnStart);
        }

        private void CreateServices()
        {
            ISaveStorage storage = useInMemorySaves
                ? new InMemorySaveStorage()
                : (ISaveStorage)new FileSaveStorage();

            var saveService = new SaveService(storage);
            ServiceLocator.Register(saveService);

            if (!TryGetComponent(out SceneLoader sceneLoader))
            {
                sceneLoader = gameObject.AddComponent<SceneLoader>();
            }

            ServiceLocator.Register(sceneLoader);

            if (sceneCatalog != null)
            {
                ServiceLocator.Register(sceneCatalog);
            }

            ServiceLocator.Register(timeScale);

            stateMachine = new GameStateMachine();
            stateMachine.StateChanged += (previous, current) =>
                GameLog.Info(LogChannel.Core, $"Game state {previous} -> {current}.", this);
            RegisterGameStates();
            ServiceLocator.Register(stateMachine);

            if (inputReader != null)
            {
                inputReader.Initialize();
                inputReader.PausePerformed += RequestPauseToggle;
                ServiceLocator.Register(inputReader);
            }
            else
            {
                GameLog.Warn(LogChannel.Input, "No InputReader assigned; input will be unavailable.", this);
            }
        }

        private void RegisterGameStates()
        {
            stateMachine.Register(GameStateId.Booting, new DelegateGameState());

            stateMachine.Register(GameStateId.MainMenu, new DelegateGameState(
                onEnter: EnableUIInput));

            stateMachine.Register(GameStateId.Loading, new DelegateGameState(
                onEnter: DisableInput));

            stateMachine.Register(GameStateId.Playing, new DelegateGameState(
                onEnter: () =>
                {
                    timeScale.BaseScale = 1f;
                    EnableGameplayInput();
                }));

            // Only the base scale moves here. A hit-stop dip is a separate factor that expires on
            // its own, so pausing mid-impact cannot leave time running slow, and a dip expiring
            // cannot unpause the game.
            stateMachine.Register(GameStateId.Paused, new DelegateGameState(
                onEnter: () =>
                {
                    timeScale.ClearDip();
                    timeScale.BaseScale = 0f;
                    EnableUIInput();
                },
                onExit: () => timeScale.BaseScale = 1f));

            stateMachine.ChangeTo(GameStateId.Booting);
        }

        // Null-conditional (?.) compares by reference and skips Unity's overloaded == , so it is the
        // wrong tool for a UnityEngine.Object field. These wrappers keep the comparison honest.
        private void EnableGameplayInput()
        {
            if (inputReader != null)
            {
                inputReader.EnableGameplay();
            }
        }

        private void EnableUIInput()
        {
            if (inputReader != null)
            {
                inputReader.EnableUI();
            }
        }

        private void DisableInput()
        {
            if (inputReader != null)
            {
                inputReader.DisableAll();
            }
        }

        private void LoadFirstScene()
        {
            stateMachine.ChangeTo(GameStateId.Loading);

            SceneLoader loader = ServiceLocator.Get<SceneLoader>();
            loader.TransitionToGameplayScene(firstScene, () => stateMachine.ChangeTo(GameStateId.Playing));
        }

        /// <summary>
        /// Records that pause was pressed. Deliberately does no more than that.
        /// </summary>
        /// <remarks>
        /// This runs inside an Input System action callback, and entering or leaving the paused
        /// state enables and disables action maps. Changing the enabled state of actions while
        /// actions are being processed is not something the Input System guarantees, and it is why
        /// simply re-enabling the pause action was not enough to make pause releasable: the call
        /// was being made in the one context where it may not take effect.
        ///
        /// So the callback sets a flag and <see cref="Update"/> does the work, outside the input
        /// pipeline. Any future input that reconfigures maps should follow the same shape.
        /// </remarks>
        private void RequestPauseToggle() => pauseTogglePending = true;

        private void ApplyPendingPauseToggle()
        {
            if (!pauseTogglePending)
            {
                return;
            }

            pauseTogglePending = false;

            if (stateMachine == null)
            {
                return;
            }

            switch (stateMachine.Current)
            {
                case GameStateId.Playing:
                    stateMachine.ChangeTo(GameStateId.Paused);
                    break;
                case GameStateId.Paused:
                    stateMachine.ChangeTo(GameStateId.Playing);
                    break;
            }
        }
    }
}

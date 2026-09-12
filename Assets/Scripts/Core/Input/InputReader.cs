using System;
using Frieren.Core.Debugging;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Frieren.Core.Input
{
    /// <summary>
    /// The single place raw Input System callbacks are turned into game-facing events and values.
    /// </summary>
    /// <remarks>
    /// Deliberately not the Input System's generated C# wrapper. That file is regenerated from the
    /// asset, is checked in, and conflicts on every merge where two people touched bindings; it
    /// also hard-couples every consumer to the exact action names. Resolving actions by name once
    /// at initialisation costs a handful of lookups and keeps the rest of the game depending only
    /// on this type.
    ///
    /// It is a ScriptableObject so the player controller, camera and UI can all reference the same
    /// asset in the inspector without a runtime lookup or a singleton.
    ///
    /// Milestone 1 only wires the plumbing - nothing consumes these events until the player
    /// controller arrives in Milestone 2.
    /// </remarks>
    [CreateAssetMenu(menuName = "Frieren/Core/Input Reader", fileName = "InputReader", order = 20)]
    public sealed class InputReader : ScriptableObject
    {
        private const string GameplayMapName = "Gameplay";
        private const string UIMapName = "UI";

        [SerializeField] private InputActionAsset actions;

        private InputActionMap gameplayMap;
        private InputActionMap uiMap;

        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction jumpAction;
        private InputAction sprintAction;
        private InputAction dodgeAction;
        private InputAction interactAction;
        private InputAction castAction;
        private InputAction blockAction;
        private InputAction spellWheelAction;
        private InputAction lockOnAction;
        private InputAction pauseAction;

        public bool IsInitialized { get; private set; }

        public Vector2 MoveInput { get; private set; }

        public Vector2 LookInput { get; private set; }

        /// <summary>
        /// True when the most recent look input came from a pointer.
        /// </summary>
        /// <remarks>
        /// A mouse reports a delta already accumulated over the frame; a gamepad stick reports a
        /// position that has to be multiplied by delta time to become a rate. Applying either rule
        /// to both devices is wrong - a stick treated as a delta makes camera speed depend on frame
        /// rate, and a mouse scaled by delta time makes it depend on it the other way. Consumers
        /// branch on this rather than trying to guess from the magnitude.
        /// </remarks>
        public bool LookIsPointerDelta { get; private set; }

        public bool SprintHeld { get; private set; }

        /// <summary>True while the block button is held, for anything that asks rather than listens.</summary>
        public bool BlockHeld { get; private set; }

        public bool SpellWheelHeld { get; private set; }

        public event Action<Vector2> MoveChanged;

        public event Action<Vector2> LookChanged;

        public event Action<bool> SprintChanged;

        public event Action JumpPerformed;

        public event Action DodgePerformed;

        public event Action InteractPerformed;

        public event Action CastPerformed;

        /// <summary>Raised when the cast button is released. Ends a channelled spell.</summary>
        public event Action CastReleased;

        /// <summary>Raised while the block button goes down. Raises a ward for as long as it is held.</summary>
        public event Action BlockPerformed;

        public event Action BlockReleased;

        /// <summary>Raised when the spell wheel opens. It stays open until <see cref="SpellWheelReleased"/>.</summary>
        public event Action SpellWheelPerformed;

        public event Action SpellWheelReleased;

        /// <summary>Raised when the lock-on button goes down. A toggle, not a hold.</summary>
        public event Action LockOnPerformed;

        public event Action PausePerformed;

        /// <summary>
        /// Resolves actions and subscribes. Safe to call repeatedly: it tears down any previous
        /// wiring first, which matters in the editor where a ScriptableObject's runtime fields can
        /// survive leaving play mode when domain reload is disabled. Listeners registered on the
        /// public events are kept; only <see cref="Dispose"/> drops those.
        /// </summary>
        public void Initialize()
        {
            // Unwire, deliberately not Dispose. A scene that is not Boot loads its own components
            // first and SceneBootstrapGuard pulls Boot in additively afterwards, so by the time the
            // Bootstrapper initialises this asset the camera rig and player have already
            // subscribed. Dropping their handlers here left the camera unable to look around.
            Unwire();

            if (actions == null)
            {
                GameLog.Error(LogChannel.Input, $"{name} has no InputActionAsset assigned.", this);
                return;
            }

            gameplayMap = actions.FindActionMap(GameplayMapName, throwIfNotFound: false);
            uiMap = actions.FindActionMap(UIMapName, throwIfNotFound: false);

            if (gameplayMap == null)
            {
                GameLog.Error(LogChannel.Input, $"Action map '{GameplayMapName}' not found in {actions.name}.", this);
                return;
            }

            moveAction = Resolve(gameplayMap, "Move");
            lookAction = Resolve(gameplayMap, "Look");
            jumpAction = Resolve(gameplayMap, "Jump");
            sprintAction = Resolve(gameplayMap, "Sprint");
            dodgeAction = Resolve(gameplayMap, "Dodge");
            interactAction = Resolve(gameplayMap, "Interact");
            castAction = Resolve(gameplayMap, "Cast");
            blockAction = Resolve(gameplayMap, "Block");
            spellWheelAction = Resolve(gameplayMap, "SpellWheel");
            lockOnAction = Resolve(gameplayMap, "LockOn");
            pauseAction = Resolve(gameplayMap, "Pause");

            Bind(moveAction, OnMove, OnMove);
            Bind(lookAction, OnLook, OnLook);
            Bind(jumpAction, OnJump);
            Bind(sprintAction, OnSprintStarted, OnSprintCanceled);
            Bind(dodgeAction, OnDodge);
            Bind(interactAction, OnInteract);
            Bind(castAction, OnCast, OnCastReleased);
            Bind(blockAction, OnBlock, OnBlockReleased);
            Bind(spellWheelAction, OnSpellWheel, OnSpellWheelReleased);
            Bind(lockOnAction, OnLockOn);
            Bind(pauseAction, OnPause);

            IsInitialized = true;
            GameLog.Info(LogChannel.Input, $"{name} initialised from {actions.name}.", this);
        }

        public void EnableGameplay()
        {
            uiMap?.Disable();
            gameplayMap?.Enable();
        }

        public void EnableUI()
        {
            gameplayMap?.Disable();
            uiMap?.Enable();

            // Pause lives in the Gameplay map, so disabling that map disables the only input that
            // can leave the paused state. Combined with a zero time scale that is a hard lock: one
            // press of Escape and the game never comes back. Re-enable the single action.
            pauseAction?.Enable();

            // Disabling a map cancels its in-progress actions, but clear the cached values too so a
            // held stick cannot leak a stale direction into the frame gameplay resumes.
            ResetValues();
        }

        public void DisableAll()
        {
            gameplayMap?.Disable();
            uiMap?.Disable();

            // Explicit, because EnableUI can leave this action enabled on its own. Disabling the
            // map should already cover it; saying so here means the invariant does not depend on
            // that detail.
            pauseAction?.Disable();

            ResetValues();
        }

        /// <summary>
        /// Full teardown: unwires the actions and drops every listener. Called when the asset is
        /// unloaded, which in the editor is leaving play mode. Anything short of that wants
        /// <see cref="Unwire"/>, because listeners outlive a re-initialisation.
        /// </summary>
        public void Dispose()
        {
            Unwire();

            // Listeners from a previous play session would otherwise point at destroyed objects.
            MoveChanged = null;
            LookChanged = null;
            SprintChanged = null;
            JumpPerformed = null;
            DodgePerformed = null;
            InteractPerformed = null;
            CastPerformed = null;
            CastReleased = null;
            BlockPerformed = null;
            BlockReleased = null;
            SpellWheelPerformed = null;
            SpellWheelReleased = null;
            LockOnPerformed = null;
            PausePerformed = null;
        }

        /// <summary>Unsubscribes from the actions and disables the maps, leaving listeners intact.</summary>
        private void Unwire()
        {
            Unbind(moveAction, OnMove, OnMove);
            Unbind(lookAction, OnLook, OnLook);
            Unbind(jumpAction, OnJump);
            Unbind(sprintAction, OnSprintStarted, OnSprintCanceled);
            Unbind(dodgeAction, OnDodge);
            Unbind(interactAction, OnInteract);
            Unbind(castAction, OnCast, OnCastReleased);
            Unbind(blockAction, OnBlock, OnBlockReleased);
            Unbind(spellWheelAction, OnSpellWheel, OnSpellWheelReleased);
            Unbind(lockOnAction, OnLockOn);
            Unbind(pauseAction, OnPause);

            gameplayMap?.Disable();
            uiMap?.Disable();

            moveAction = null;
            lookAction = null;
            jumpAction = null;
            sprintAction = null;
            dodgeAction = null;
            interactAction = null;
            castAction = null;
            blockAction = null;
            spellWheelAction = null;
            lockOnAction = null;
            pauseAction = null;
            gameplayMap = null;
            uiMap = null;

            ResetValues();
            IsInitialized = false;
        }

        private void OnDisable() => Dispose();

        private void ResetValues()
        {
            MoveInput = Vector2.zero;
            LookInput = Vector2.zero;
            LookIsPointerDelta = false;
            SprintHeld = false;
            BlockHeld = false;
            SpellWheelHeld = false;
        }

        private InputAction Resolve(InputActionMap map, string actionName)
        {
            InputAction action = map.FindAction(actionName, throwIfNotFound: false);

            if (action == null)
            {
                GameLog.Warn(LogChannel.Input, $"Action '{actionName}' not found in map '{map.name}'.", this);
            }

            return action;
        }

        private static void Bind(InputAction action, Action<InputAction.CallbackContext> performed,
            Action<InputAction.CallbackContext> canceled = null)
        {
            if (action == null)
            {
                return;
            }

            action.performed += performed;

            if (canceled != null)
            {
                action.canceled += canceled;
            }
        }

        private static void Unbind(InputAction action, Action<InputAction.CallbackContext> performed,
            Action<InputAction.CallbackContext> canceled = null)
        {
            if (action == null)
            {
                return;
            }

            action.performed -= performed;

            if (canceled != null)
            {
                action.canceled -= canceled;
            }
        }

        private void OnMove(InputAction.CallbackContext context)
        {
            MoveInput = context.ReadValue<Vector2>();
            MoveChanged?.Invoke(MoveInput);
        }

        private void OnLook(InputAction.CallbackContext context)
        {
            LookInput = context.ReadValue<Vector2>();
            LookIsPointerDelta = context.control != null && context.control.device is Pointer;
            LookChanged?.Invoke(LookInput);
        }

        private void OnSprintStarted(InputAction.CallbackContext context)
        {
            SprintHeld = true;
            SprintChanged?.Invoke(true);
        }

        private void OnSprintCanceled(InputAction.CallbackContext context)
        {
            SprintHeld = false;
            SprintChanged?.Invoke(false);
        }

        private void OnJump(InputAction.CallbackContext context) => JumpPerformed?.Invoke();

        private void OnDodge(InputAction.CallbackContext context) => DodgePerformed?.Invoke();

        private void OnInteract(InputAction.CallbackContext context) => InteractPerformed?.Invoke();

        private void OnCast(InputAction.CallbackContext context) => CastPerformed?.Invoke();

        private void OnCastReleased(InputAction.CallbackContext context) => CastReleased?.Invoke();

        private void OnBlock(InputAction.CallbackContext context)
        {
            BlockHeld = true;
            BlockPerformed?.Invoke();
        }

        private void OnBlockReleased(InputAction.CallbackContext context)
        {
            BlockHeld = false;
            BlockReleased?.Invoke();
        }

        private void OnSpellWheel(InputAction.CallbackContext context)
        {
            SpellWheelHeld = true;
            SpellWheelPerformed?.Invoke();
        }

        private void OnSpellWheelReleased(InputAction.CallbackContext context)
        {
            SpellWheelHeld = false;
            SpellWheelReleased?.Invoke();
        }

        private void OnLockOn(InputAction.CallbackContext context) => LockOnPerformed?.Invoke();

        private void OnPause(InputAction.CallbackContext context) => PausePerformed?.Invoke();
    }
}

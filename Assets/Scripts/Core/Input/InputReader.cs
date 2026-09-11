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
        private InputAction pauseAction;

        public bool IsInitialized { get; private set; }

        public Vector2 MoveInput { get; private set; }

        public Vector2 LookInput { get; private set; }

        public bool SprintHeld { get; private set; }

        public event Action<Vector2> MoveChanged;

        public event Action<Vector2> LookChanged;

        public event Action<bool> SprintChanged;

        public event Action JumpPerformed;

        public event Action DodgePerformed;

        public event Action InteractPerformed;

        public event Action CastPerformed;

        public event Action PausePerformed;

        /// <summary>
        /// Resolves actions and subscribes. Safe to call repeatedly: it tears down any previous
        /// wiring first, which matters in the editor where a ScriptableObject's runtime fields can
        /// survive leaving play mode when domain reload is disabled.
        /// </summary>
        public void Initialize()
        {
            Dispose();

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
            pauseAction = Resolve(gameplayMap, "Pause");

            Bind(moveAction, OnMove, OnMove);
            Bind(lookAction, OnLook, OnLook);
            Bind(jumpAction, OnJump);
            Bind(sprintAction, OnSprintStarted, OnSprintCanceled);
            Bind(dodgeAction, OnDodge);
            Bind(interactAction, OnInteract);
            Bind(castAction, OnCast);
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
        }

        public void DisableAll()
        {
            gameplayMap?.Disable();
            uiMap?.Disable();
            ResetValues();
        }

        /// <summary>Unsubscribes, disables the maps and drops all listeners.</summary>
        public void Dispose()
        {
            Unbind(moveAction, OnMove, OnMove);
            Unbind(lookAction, OnLook, OnLook);
            Unbind(jumpAction, OnJump);
            Unbind(sprintAction, OnSprintStarted, OnSprintCanceled);
            Unbind(dodgeAction, OnDodge);
            Unbind(interactAction, OnInteract);
            Unbind(castAction, OnCast);
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
            pauseAction = null;
            gameplayMap = null;
            uiMap = null;

            // Listeners from a previous play session would otherwise point at destroyed objects.
            MoveChanged = null;
            LookChanged = null;
            SprintChanged = null;
            JumpPerformed = null;
            DodgePerformed = null;
            InteractPerformed = null;
            CastPerformed = null;
            PausePerformed = null;

            ResetValues();
            IsInitialized = false;
        }

        private void OnDisable() => Dispose();

        private void ResetValues()
        {
            MoveInput = Vector2.zero;
            LookInput = Vector2.zero;
            SprintHeld = false;
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

        private void OnPause(InputAction.CallbackContext context) => PausePerformed?.Invoke();
    }
}

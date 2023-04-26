using AggroBird.ReflectionDebugConsole;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace AggroBird.GameFramework
{
    public enum InputMode
    {
        KeyboardMouse,
        Controller,
    }

    [DefaultExecutionOrder(-99999)]
    public abstract class AppInstance : MonoBehaviour
    {
        // Singleton instance
        public static bool IsInitialized => instance;
        private static AppInstance instance;
        public static AppInstance Instance
        {
            get
            {
                if (Application.isPlaying && !instance)
                {
                    throw new FatalGameException("Application has not been initialized yet");
                }
                return instance;
            }
        }
        public static bool TryGetInstance<T>(out T instance) where T : AppInstance => instance = AppInstance.instance as T;

        // Input
        public bool LockCursor
        {
            get => lockCursor && !uiRequiresInput;
            set
            {
                lockCursor = value;
                if (!lockCursor)
                {
                    OnLoseFocus();
                }
            }
        }
        [SerializeField]
        private bool lockCursor = true;
        private bool uiRequiresInput = false;

        public bool HasFocus { get; private set; }

        public virtual bool InputEnabled => HasFocus && inputEnabled && !uiRequiresInput;
        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
        }
        private bool inputEnabled = true;
        private InputMode inputMode = InputMode.KeyboardMouse;
        public InputMode InputMode => inputMode;

        // Players
        public abstract int PlayerCount { get; }
        public abstract Player GetPlayer(int index);
        public bool TryGetPlayer<T>(int index, out T player) where T : Player
        {
            if (index >= 0 && index < PlayerCount && GetPlayer(index) is T casted)
            {
                player = casted;
                return true;
            }

            player = null;
            return false;
        }


        public virtual void Initialize()
        {
            if (instance) throw new FatalGameException("Application instance has already been initialized");
            DontDestroyOnLoad(gameObject);
            instance = this;

            inputMode = Keyboard.current != null ? InputMode.KeyboardMouse : InputMode.Controller;

            DebugConsole.onConsoleFocusChange += OnDebugConsoleFocus;
        }



        public static event System.Action onUpdate;
        protected virtual void Update()
        {
            for (int i = 0; i < PlayerCount; i++)
            {
                if (TryGetPlayer(i, out Player player))
                {
                    player.UpdateInput();
                }
            }

            onUpdate?.Invoke();
        }

        public static event System.Action onLateUpdate;
        protected virtual void LateUpdate()
        {
            for (int i = 0; i < PlayerCount; i++)
            {
                if (TryGetPlayer(i, out Player player))
                {
                    player.UpdateUserInterface();
                }
            }

            onLateUpdate?.Invoke();

            // Check if UI still requires input
            uiRequiresInput = false;
            for (int i = 0; i < PlayerCount; i++)
            {
                if (TryGetPlayer(i, out Player player) && player.TryGetUserInterface(out UserInterface userInterface))
                {
                    uiRequiresInput |= userInterface.AllowInput;
                }
            }

            // Gain focus on mouse button click
            if (!HasFocus && !DebugConsole.HasFocus)
            {
                Mouse mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.isPressed)
                {
                    OnGainFocus();
                }
            }

            // Editor specific remove focus on escape
            if (HasFocus && Application.isEditor)
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null && keyboard.escapeKey.isPressed)
                {
                    OnLoseFocus();
                }
            }

            if (HasFocus)
            {
                UpdateInputMode();

                SetCursorLocked(lockCursor && (inputMode == InputMode.Controller || !uiRequiresInput));
            }
        }

        public event System.Action<InputMode> onInputModeChanged;
        private void UpdateInputMode()
        {
            if (inputMode == InputMode.KeyboardMouse)
            {
                Gamepad gamepad = Gamepad.current;
                if (gamepad != null)
                {
                    foreach (var control in Gamepad.current.allControls)
                    {
                        if (!control.synthetic && control is ButtonControl button && button.wasPressedThisFrame)
                        {
                            SwitchInputMode(InputMode.Controller);
                            return;
                        }
                    }

                    if (gamepad.leftStick.ReadValue().magnitude > 0.1f || gamepad.rightStick.ReadValue().magnitude > 0.1f)
                    {
                        SwitchInputMode(InputMode.Controller);
                        return;
                    }
                }
            }
            else
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.anyKey.wasPressedThisFrame)
                    {
                        SwitchInputMode(InputMode.KeyboardMouse);
                        return;
                    }
                }

                Mouse mouse = Mouse.current;
                if (mouse != null)
                {
                    if (mouse.delta.ReadValue().magnitude > 0.1f)
                    {
                        SwitchInputMode(InputMode.KeyboardMouse);
                        return;
                    }

                    if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
                    {
                        SwitchInputMode(InputMode.KeyboardMouse);
                        return;
                    }
                }
            }
        }
        private void SwitchInputMode(InputMode newMode)
        {
            if (inputMode != newMode)
            {
                inputMode = newMode;
                onInputModeChanged?.Invoke(inputMode);
            }
        }

        public static event System.Action onShutdown;
        protected virtual void Shutdown()
        {
            onShutdown?.Invoke();
        }

        protected virtual void OnDestroy()
        {
            DebugConsole.onConsoleFocusChange -= OnDebugConsoleFocus;

            Shutdown();

            instance = null;
        }


        private void OnApplicationFocus(bool focus)
        {
            if (!focus) OnLoseFocus();
        }
        private void OnDebugConsoleFocus(bool focus)
        {
            if (focus) OnLoseFocus();
        }


        protected virtual void OnGainFocus()
        {
            HasFocus = true;
        }
        protected virtual void OnLoseFocus()
        {
            HasFocus = false;
            SetCursorLocked(false);
        }

        private void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
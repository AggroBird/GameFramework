using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AggroBird.GameFramework
{
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

        public bool HasFocus { get; private set; } = true;

        public virtual bool InputEnabled => HasFocus && inputEnabled && !uiRequiresInput;
        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
        }
        private bool inputEnabled = true;

        private Type debugConsoleClass;
        private EventInfo onConsoleFocusChange;
        private Delegate onConsoleFocusChangeDelegate;
        private Func<bool> debugConsoleHasFocusGetMethod;

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

        protected virtual PlatformProfile InstantiatePlatformProfile()
        {
            return ScriptableObject.CreateInstance<StandalonePlatformProfile>();
        }

        public PlatformProfile PlatformProfile { get; private set; }


        public virtual void Initialize()
        {
            if (instance) throw new FatalGameException("Application instance has already been initialized");
            DontDestroyOnLoad(gameObject);
            instance = this;

            PlatformProfile = InstantiatePlatformProfile();
            if (!PlatformProfile) throw new FatalGameException("Failed to create platform profile");
            PlatformProfile.Initialize();

            // Bind to debug console through reflection
            debugConsoleClass = Type.GetType("AggroBird.ReflectionDebugConsole.DebugConsole, AggroBird.ReflectionDebugConsole, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
            if (debugConsoleClass != null)
            {
                onConsoleFocusChange = debugConsoleClass.GetEvent("OnConsoleFocusChange");
                if (onConsoleFocusChange != null)
                {
                    MethodInfo callback = typeof(AppInstance).GetMethod(nameof(OnDebugConsoleFocus), BindingFlags.NonPublic | BindingFlags.Instance);
                    onConsoleFocusChangeDelegate = Delegate.CreateDelegate(onConsoleFocusChange.EventHandlerType, this, callback);
                    onConsoleFocusChange.AddEventHandler(this, onConsoleFocusChangeDelegate);
                }

                PropertyInfo debugConsoleHasFocus = debugConsoleClass.GetProperty("HasFocus");
                if (debugConsoleHasFocus != null)
                {
                    debugConsoleHasFocusGetMethod = (Func<bool>)debugConsoleHasFocus.GetMethod.CreateDelegate(typeof(Func<bool>));
                }
            }
        }


        private InputSettings.UpdateMode InputUpdateMode
        {
            get
            {
                if (InputSystem.settings)
                {
                    return InputSystem.settings.updateMode;
                }
                else
                {
                    return InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
                }
            }
        }

        public static event Action OnUpdate;
        protected virtual void Update()
        {
            if (InputUpdateMode == InputSettings.UpdateMode.ProcessEventsInDynamicUpdate)
            {
                for (int i = 0; i < PlayerCount; i++)
                {
                    if (TryGetPlayer(i, out Player player))
                    {
                        player.UpdateInput(i);
                    }
                }
            }
            OnUpdate?.Invoke();
        }

        public static event Action OnFixedUpdate;
        protected virtual void FixedUpdate()
        {
            if (InputUpdateMode == InputSettings.UpdateMode.ProcessEventsInFixedUpdate)
            {
                for (int i = 0; i < PlayerCount; i++)
                {
                    if (TryGetPlayer(i, out Player player))
                    {
                        player.UpdateInput(i);
                    }
                }
            }
            OnFixedUpdate?.Invoke();
        }

        public static event Action OnLateUpdate;
        protected virtual void LateUpdate()
        {
            for (int i = 0; i < PlayerCount; i++)
            {
                if (TryGetPlayer(i, out Player player))
                {
                    player.UpdateUserInterface();
                }
            }
            OnLateUpdate?.Invoke();

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
            if (!HasFocus && (debugConsoleHasFocusGetMethod == null || !debugConsoleHasFocusGetMethod.Invoke()))
            {
                Mouse mouse = Mouse.current;
                if (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed))
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
                PlatformProfile.UpdateInputMode();

                SetCursorLocked(lockCursor && (PlatformProfile.ActiveInputMode == InputMode.Gamepad || !uiRequiresInput));
            }
        }


        public static event Action OnShutdown;
        public static event Action OnShutdownEditor;
        protected virtual void Shutdown()
        {
            OnShutdown?.Invoke();
            if (Application.isEditor)
            {
                OnShutdownEditor?.Invoke();
            }
        }

        protected virtual void OnDestroy()
        {
            if (onConsoleFocusChange != null)
            {
                onConsoleFocusChange.RemoveEventHandler(this, onConsoleFocusChangeDelegate);
            }

            Shutdown();

            if (PlatformProfile)
            {
                Destroy(PlatformProfile);
            }
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

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
        [field: SerializeField]
        public bool LockCursor { get; protected set; } = true;
        private bool uiRequiresInput = false;

        public bool HasFocus { get; private set; } = true;
        public virtual bool AllowRegainFocus => debugConsoleHasFocusGetMethod == null || !debugConsoleHasFocusGetMethod.Invoke();

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
                    debugConsoleHasFocusGetMethod = debugConsoleHasFocus.GetMethod.CreateDelegate(typeof(Func<bool>)) as Func<bool>;
                }
            }
        }


        private InputSettings.UpdateMode InputUpdateMode => InputSystem.settings ? InputSystem.settings.updateMode : InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;

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
        }

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

                UpdateFocusInput();
            }
        }

        protected virtual void LateUpdate()
        {
            for (int i = 0; i < PlayerCount; i++)
            {
                if (TryGetPlayer(i, out Player player))
                {
                    player.UpdateUserInterface();
                }
            }

            // Check if UI still requires input
            uiRequiresInput = false;
            for (int i = 0; i < PlayerCount; i++)
            {
                if (TryGetPlayer(i, out Player player) && player.TryGetUserInterface(out UserInterface userInterface))
                {
                    uiRequiresInput |= userInterface.AllowInput;
                }
            }

            if (InputUpdateMode == InputSettings.UpdateMode.ProcessEventsInDynamicUpdate)
            {
                UpdateFocusInput();
            }

            if (HasFocus)
            {
                PlatformProfile.UpdateInputMode();

                SetCursorLocked(LockCursor && (PlatformProfile.ActiveInputMode == InputMode.Gamepad || !uiRequiresInput));
            }
        }


        private void UpdateFocusInput()
        {
            // Gain focus on mouse button click
            if (!HasFocus && AllowRegainFocus && Mouse.current is Mouse mouse)
            {
                if (mouse.leftButton.isPressed || mouse.rightButton.isPressed)
                {
                    SetHasFocus(true);
                }
            }

#if UNITY_EDITOR
            // Editor specific remove focus on escape
            if (HasFocus && Application.isEditor && !uiRequiresInput)
            {
                if (Keyboard.current is Keyboard keyboard && keyboard.escapeKey.isPressed)
                {
                    SetHasFocus(false);
                }
            }
#endif
        }


        protected virtual void Shutdown()
        {

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
            if (!focus)
            {
                SetHasFocus(false);
            }
            else if (AllowRegainFocus)
            {
                SetHasFocus(true);
            }
        }
        private void OnDebugConsoleFocus(bool focus)
        {
            if (focus)
            {
                SetHasFocus(false);
            }
        }


        private void SetHasFocus(bool focus)
        {
            if (focus)
            {
                if (!HasFocus)
                {
                    HasFocus = true;
                    OnGainFocus();
                }
            }
            else
            {
                if (HasFocus)
                {
                    HasFocus = false;
                    SetCursorLocked(false);
                    OnLoseFocus();
                }
            }
        }

        protected virtual void OnGainFocus()
        {

        }
        protected virtual void OnLoseFocus()
        {

        }

        private void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}

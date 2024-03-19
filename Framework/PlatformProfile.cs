using System;
using UnityEngine;

namespace AggroBird.GameFramework
{
    public enum InputMode
    {
        KeyboardMouse,
        Gamepad,
    }

    public abstract class PlatformProfile : ScriptableObject
    {
        public event Action<InputMode> OnInputModeChanged;

        public abstract bool SupportsMouseKeyboard { get; }
        public abstract InputMode ActiveInputMode { get; }

        public abstract void Initialize();
        public abstract void Shutdown();

        public abstract void UpdateInputMode();

        public abstract Controller CreateController(Player player);

        protected void InputModeChanged(InputMode inputMode)
        {
            OnInputModeChanged?.Invoke(inputMode);
        }
    }
}
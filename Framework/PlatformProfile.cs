using System;
using UnityEngine;

namespace AggroBird.GameFramework
{
    public enum InputMode
    {
        KeyboardMouse,
        Controller,
    }

    public abstract class PlatformProfile : ScriptableObject
    {
        public event Action<InputMode> OnInputModeChanged;

        public abstract bool SupportsMouseKeyboard { get; }
        public abstract InputMode ActiveInputMode { get; }

        public abstract void Initialize();
        public abstract void UpdateInputMode();

        public abstract Controller CreateController(int player);

        protected void InputModeChanged(InputMode inputMode)
        {
            OnInputModeChanged?.Invoke(inputMode);
        }
    }
}
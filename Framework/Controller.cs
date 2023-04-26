using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace AggroBird.GameFramework
{
    public enum ButtonState
    {
        None = 0,
        Pressed,
        Held,
        Released,
    }

    public static class InputSystemUtility
    {
        public static ButtonControl GetMouseButton(this Mouse mouse, MouseButton mouseButton)
        {
            switch (mouseButton)
            {
                case MouseButton.Left:
                    return mouse.leftButton;
                case MouseButton.Right:
                    return mouse.rightButton;
                case MouseButton.Middle:
                    return mouse.middleButton;
                case MouseButton.Forward:
                    return mouse.forwardButton;
                case MouseButton.Back:
                    return mouse.backButton;
            }
            throw new System.ArgumentException("Invalid mouse button");
        }
    }

    public readonly struct ButtonStateObject : System.IEquatable<ButtonStateObject>
    {
        public ButtonStateObject(ButtonState state)
        {
            this.state = state;
        }

        public readonly ButtonState state;

        public bool IsPressed => state == ButtonState.Pressed;
        public bool IsHeld => state == ButtonState.Held;
        public bool IsReleased => state == ButtonState.Released;

        public static implicit operator ButtonStateObject(ButtonState state)
        {
            return new ButtonStateObject(state);
        }
        public static implicit operator ButtonState(ButtonStateObject state)
        {
            return state.state;
        }
        public static implicit operator bool(ButtonStateObject state)
        {
            return state.state == ButtonState.Pressed || state.state == ButtonState.Held;
        }

        public bool Equals(ButtonStateObject other)
        {
            return state.Equals(other.state);
        }
        public override bool Equals(object obj)
        {
            return obj is ButtonStateObject other && Equals(other);
        }

        public override int GetHashCode()
        {
            return state.GetHashCode();
        }
        public override string ToString()
        {
            return state.ToString();
        }

        public static bool operator ==(ButtonStateObject lhs, ButtonStateObject rhs)
        {
            return lhs.state == rhs.state;
        }
        public static bool operator !=(ButtonStateObject lhs, ButtonStateObject rhs)
        {
            return lhs.state != rhs.state;
        }
    }

    public abstract class ControllerInputAction<T>
    {
        public ControllerInputAction(T defaultValue = default)
        {
            this.defaultValue = defaultValue;
        }

        public void Use()
        {
            value = defaultValue;
        }
        public T GetValue(bool use)
        {
            T result = value;
            if (use) Use();
            return result;
        }

        public T Value => value;

        protected T value = default;
        protected AsyncReactiveProperty<T> reactive = new(default);
        private readonly T defaultValue;

        public IReadOnlyAsyncReactiveProperty<T> Reactive => reactive;

        public static implicit operator T(ControllerInputAction<T> action)
        {
            return action.value;
        }
    }

    public abstract class Controller : ScriptableObject
    {
        protected struct ButtonStateObjectMask
        {
            public ButtonStateObject value;
            public bool wasModified;

            public static implicit operator ButtonStateObject(ButtonStateObjectMask buttonStateObjectResult)
            {
                return buttonStateObjectResult.value;
            }

            public static ButtonStateObjectMask operator |(ButtonStateObjectMask lhs, ButtonControl control)
            {
                if (!lhs.wasModified)
                {
                    if (control.wasPressedThisFrame)
                    {
                        lhs.value = ButtonState.Pressed;
                        lhs.wasModified = true;
                    }
                    else if (control.wasReleasedThisFrame)
                    {
                        lhs.value = ButtonState.Released;
                        lhs.wasModified = true;
                    }
                    else if (control.isPressed)
                    {
                        lhs.value = ButtonState.Held;
                        lhs.wasModified = true;
                    }
                }
                return lhs;
            }
        }

        public Vector2 CameraInput { get; protected set; }

        protected sealed class WritableInputAction<T> : ControllerInputAction<T>
        {
            public WritableInputAction(T defaultValue = default) : base(defaultValue)
            {

            }

            private readonly EqualityComparer<T> comparer = EqualityComparer<T>.Default;

            public new T Value
            {
                get => value;
                set
                {
                    if (!comparer.Equals(this.value, value))
                    {
                        reactive.Value = this.value = value;
                    }
                }
            }
        }

        protected readonly WritableInputAction<bool> confirm = new();
        protected readonly WritableInputAction<bool> cancel = new();
        protected readonly WritableInputAction<MoveDirection> direction = new(MoveDirection.None);

        public ControllerInputAction<bool> Confirm => confirm;
        public ControllerInputAction<bool> Cancel => cancel;
        public ControllerInputAction<MoveDirection> Direction => direction;


        public virtual void UpdateInput(Player player, bool inputEnabled)
        {

        }

        public virtual bool GetInputGlyph(int index, out Sprite background, out string text)
        {
            background = null;
            text = string.Empty;
            return false;
        }
    }
}

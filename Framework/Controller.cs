using AggroBird.UnityExtend;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using GamepadButtonCode = UnityEngine.InputSystem.LowLevel.GamepadButton;
using KeyCode = UnityEngine.InputSystem.Key;
using MouseButtonCode = UnityEngine.InputSystem.LowLevel.MouseButton;

namespace AggroBird.GameFramework
{
    public enum GamepadStickCode
    {
        LeftStick,
        RightStick,
    }

    public enum AxisDirection
    {
        Horizontal,
        Vertical,
    }

    public static class InputSystemUtility
    {
        public static StickControl GetStickControl(this Gamepad gamepad, GamepadStickCode stick)
        {
            return stick == GamepadStickCode.LeftStick ? gamepad.leftStick : gamepad.rightStick;
        }
        public static ButtonControl GetMouseButton(this Mouse mouse, MouseButtonCode mouseButton)
        {
            return mouseButton switch
            {
                MouseButtonCode.Left => mouse.leftButton,
                MouseButtonCode.Right => mouse.rightButton,
                MouseButtonCode.Middle => mouse.middleButton,
                MouseButtonCode.Forward => mouse.forwardButton,
                MouseButtonCode.Back => mouse.backButton,
                _ => throw new ArgumentException("Invalid mouse button"),
            };
        }
    }

    public enum ButtonState
    {
        None = 0,
        Pressed,
        Held,
        Released,
    }

    public enum InputDirectionValue
    {
        None = 0,
        Up,
        Right,
        Down,
        Left,
    }

    // Input elements
    [Serializable]
    public abstract class InputElement
    {
        public abstract void Update(int index = 0);

        protected static bool TryGetKeyboard(int index, out Keyboard keyboard)
        {
            if (index == 0)
            {
                keyboard = Keyboard.current;
                return keyboard != null;
            }

            keyboard = null;
            return false;
        }
        protected static bool TryGetMouse(int index, out Mouse mouse)
        {
            if (index == 0)
            {
                mouse = Mouse.current;
                return mouse != null;
            }

            mouse = null;
            return false;
        }
        protected static bool TryGetGamepad(int index, out Gamepad gamepad)
        {
            var gamepads = Gamepad.all;
            if ((uint)index < (uint)gamepads.Count)
            {
                gamepad = gamepads[index];
                return true;
            }
            gamepad = null;
            return false;
        }
    }

    // Buttons
    [Serializable]
    public abstract class InputButton : InputElement
    {
        public abstract ButtonState State { get; }

        public bool IsPressed => State == ButtonState.Pressed;
        public bool IsHeld => State == ButtonState.Held;
        public bool IsReleased => State == ButtonState.Released;

        protected static void UpdateState(ref ButtonState state, bool isPressed)
        {
            switch (state)
            {
                case ButtonState.None:
                    if (isPressed) state = ButtonState.Pressed;
                    break;
                case ButtonState.Pressed:
                    state = isPressed ? ButtonState.Held : ButtonState.Released;
                    break;
                case ButtonState.Held:
                    if (!isPressed) state = ButtonState.Released;
                    break;
                case ButtonState.Released:
                    state = isPressed ? ButtonState.Pressed : ButtonState.None;
                    break;
            }
        }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "Binary keyboard key switch (true/false)")]
    public sealed class KeyboardKey : InputButton
    {
        public KeyboardKey(KeyCode key)
        {
            Key = key;
        }

        [field: SerializeField] public KeyCode Key { get; private set; }

        public override ButtonState State => state;
        private ButtonState state;

        public override void Update(int index = 0)
        {
            if (Key != KeyCode.None && TryGetKeyboard(index, out Keyboard keyboard))
            {
                UpdateState(ref state, keyboard[Key].isPressed);
                return;
            }

            UpdateState(ref state, false);
        }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "Binary mouse button switch (true/false)")]
    public sealed class MouseButton : InputButton
    {
        public MouseButton(MouseButtonCode button)
        {
            Button = button;
        }

        [field: SerializeField] public MouseButtonCode Button { get; private set; }

        public override ButtonState State => state;
        private ButtonState state;

        public override void Update(int index = 0)
        {
            if (TryGetMouse(index, out Mouse mouse))
            {
                UpdateState(ref state, InputSystemUtility.GetMouseButton(mouse, Button).isPressed);
                return;
            }

            UpdateState(ref state, false);
        }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "Binary gamepad button switch (true/false)")]
    public sealed class GamepadButton : InputButton
    {
        public GamepadButton(GamepadButtonCode button)
        {
            Button = button;
        }

        [field: SerializeField] public GamepadButtonCode Button { get; private set; }

        public override ButtonState State => state;
        private ButtonState state;

        public override void Update(int index = 0)
        {
            if (TryGetGamepad(index, out Gamepad gamepad))
            {
                UpdateState(ref state, gamepad[Button].isPressed);
                return;
            }

            UpdateState(ref state, false);
        }
    }

    // UI Directions
    [Serializable]
    public abstract class InputDirection : InputElement
    {
        public abstract InputDirectionValue Value { get; }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "4-dimentional keyboard user interface input (up/down/left/right)")]
    public sealed class KeyboardInputDirection : InputDirection
    {
        public KeyboardInputDirection(KeyCode up, KeyCode right, KeyCode down, KeyCode left)
        {
            Up = up;
            Right = right;
            Down = down;
            Left = left;
        }

        [field: SerializeField] public KeyCode Up { get; private set; }
        [field: SerializeField] public KeyCode Right { get; private set; }
        [field: SerializeField] public KeyCode Down { get; private set; }
        [field: SerializeField] public KeyCode Left { get; private set; }

        public override InputDirectionValue Value => value;
        private InputDirectionValue value;

        public override void Update(int index = 0)
        {
            if (TryGetKeyboard(index, out Keyboard keyboard))
            {
                int y = 0;
                if (keyboard[Up].isPressed) y++;
                if (keyboard[Down].isPressed) y--;
                if (y == 1)
                {
                    value = InputDirectionValue.Up;
                    return;
                }
                if (y == -1)
                {
                    value = InputDirectionValue.Down;
                    return;
                }

                int x = 0;
                if (keyboard[Right].isPressed) x++;
                if (keyboard[Left].isPressed) x--;
                if (x == 1)
                {
                    value = InputDirectionValue.Right;
                    return;
                }
                else if (x == -1)
                {
                    value = InputDirectionValue.Left;
                    return;
                }
            }

            value = InputDirectionValue.None;
        }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "4-dimentional gamepad user interface input (up/down/left/right)")]
    public sealed class GamepadInputDirection : InputDirection
    {
        public GamepadInputDirection(GamepadButtonCode up, GamepadButtonCode right, GamepadButtonCode down, GamepadButtonCode left)
        {
            Up = up;
            Right = right;
            Down = down;
            Left = left;
        }

        [field: SerializeField] public GamepadButtonCode Up { get; private set; }
        [field: SerializeField] public GamepadButtonCode Right { get; private set; }
        [field: SerializeField] public GamepadButtonCode Down { get; private set; }
        [field: SerializeField] public GamepadButtonCode Left { get; private set; }

        public override InputDirectionValue Value => value;
        private InputDirectionValue value;

        public override void Update(int index = 0)
        {
            if (TryGetGamepad(index, out Gamepad gamepad))
            {
                int y = 0;
                if (gamepad[Up].isPressed) y++;
                if (gamepad[Down].isPressed) y--;
                if (y == 1)
                {
                    value = InputDirectionValue.Up;
                    return;
                }
                if (y == -1)
                {
                    value = InputDirectionValue.Down;
                    return;
                }

                int x = 0;
                if (gamepad[Right].isPressed) x++;
                if (gamepad[Left].isPressed) x--;
                if (x == 1)
                {
                    value = InputDirectionValue.Right;
                    return;
                }
                else if (x == -1)
                {
                    value = InputDirectionValue.Left;
                    return;
                }
            }

            value = InputDirectionValue.None;
        }
    }

    // Axes
    [Serializable]
    public abstract class InputAxis : InputElement
    {

    }

    // Linear axes
    [Serializable]
    public abstract class LinearAxis : InputAxis
    {
        public abstract float Value { get; }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "Keyboard single key axis (range 0 to 1)")]
    public sealed class KeyboardAxisButton : LinearAxis
    {
        public KeyboardAxisButton(KeyCode key)
        {
            Key = key;
        }

        [field: SerializeField] public KeyCode Key { get; private set; }

        public override float Value => value;
        private float value;

        public override void Update(int index = 0)
        {
            if (TryGetKeyboard(index, out Keyboard keyboard))
            {
                value = keyboard[Key].ReadValue();
                return;
            }

            value = 0;
        }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "Mouse single button axis (range 0 to 1)")]
    public sealed class MouseAxisButton : LinearAxis
    {
        public MouseAxisButton(MouseButtonCode button)
        {
            Button = button;
        }

        [field: SerializeField] public MouseButtonCode Button { get; private set; }

        public override float Value => value;
        private float value;

        public override void Update(int index = 0)
        {
            if (TryGetMouse(index, out Mouse mouse))
            {
                value = InputSystemUtility.GetMouseButton(mouse, Button).ReadValue();
                return;
            }

            value = 0;
        }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "Gamepad single button axis (range 0 to 1)")]
    public sealed class GamepadAxisButton : LinearAxis
    {
        public GamepadAxisButton(GamepadButtonCode button)
        {
            Button = button;
        }

        [field: SerializeField] public GamepadButtonCode Button { get; private set; }

        public override float Value => value;
        private float value;

        public override void Update(int index = 0)
        {
            if (TryGetGamepad(index, out Gamepad gamepad))
            {
                value = gamepad[Button].ReadValue();
                return;
            }

            value = 0;
        }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "Keyboard linear axis between two keys (range -1 to 1)")]
    public sealed class KeyboardKeyLinearAxis : LinearAxis
    {
        public KeyboardKeyLinearAxis(KeyCode positive, KeyCode negative)
        {
            Positive = positive;
            Negative = negative;
        }

        [field: SerializeField] public KeyCode Positive { get; private set; }
        [field: SerializeField] public KeyCode Negative { get; private set; }

        public override float Value => value;
        private float value;

        public override void Update(int index = 0)
        {
            if (TryGetKeyboard(index, out Keyboard keyboard))
            {
                value = keyboard[Positive].ReadValue() - keyboard[Negative].ReadValue();
                return;
            }

            value = 0;
        }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "Gamepad linear axis between two buttons (range -1 to 1)")]
    public sealed class GamepadButtonLinearAxis : LinearAxis
    {
        public GamepadButtonLinearAxis(GamepadButtonCode positive, GamepadButtonCode negative)
        {
            Positive = positive;
            Negative = negative;
        }

        [field: SerializeField] public GamepadButtonCode Positive { get; private set; }
        [field: SerializeField] public GamepadButtonCode Negative { get; private set; }

        public override float Value => value;
        private float value;

        public override void Update(int index = 0)
        {
            if (TryGetGamepad(index, out Gamepad gamepad))
            {
                value = gamepad[Positive].ReadValue() - gamepad[Negative].ReadValue();
                return;
            }

            value = 0;
        }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "Gamepad linear axis between along single stick axis (range -1 to 1)")]
    public sealed class GamepadStickLinearAxis : LinearAxis
    {
        public GamepadStickLinearAxis(GamepadStickCode stick, AxisDirection axis)
        {
            Stick = stick;
            Axis = axis;
        }

        [field: SerializeField] public GamepadStickCode Stick { get; private set; }
        [field: SerializeField] public AxisDirection Axis { get; private set; }

        public override float Value => value;
        private float value;

        public override void Update(int index = 0)
        {
            if (TryGetGamepad(index, out Gamepad gamepad))
            {
                StickControl stick = Stick == GamepadStickCode.RightStick ? gamepad.rightStick : gamepad.leftStick;
                value = Axis == AxisDirection.Vertical ? stick.ReadValue().y : stick.ReadValue().x;
                return;
            }

            value = 0;
        }
    }

    // Vector axes
    [Serializable]
    public abstract class VectorAxis : InputAxis
    {
        public abstract Vector2 Value { get; }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "4-dimentional keyboard vector input")]
    public sealed class KeyboardVectorAxis : VectorAxis
    {
        public KeyboardVectorAxis(KeyCode up, KeyCode right, KeyCode down, KeyCode left)
        {
            Up = up;
            Right = right;
            Down = down;
            Left = left;
        }

        [field: SerializeField] public KeyCode Up { get; private set; }
        [field: SerializeField] public KeyCode Right { get; private set; }
        [field: SerializeField] public KeyCode Down { get; private set; }
        [field: SerializeField] public KeyCode Left { get; private set; }

        public override Vector2 Value => value;
        private Vector2 value;

        public override void Update(int index = 0)
        {
            value = Vector2.zero;

            if (TryGetKeyboard(index, out Keyboard keyboard))
            {
                if (keyboard[Left].isPressed) value.x++;
                if (keyboard[Right].isPressed) value.x--;
                if (keyboard[Up].isPressed) value.y++;
                if (keyboard[Down].isPressed) value.y--;
            }
        }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "4-dimentional gamepad D-Pad vector input")]
    public sealed class GamepadDPadVectorAxis : VectorAxis
    {
        public GamepadDPadVectorAxis(GamepadButtonCode up, GamepadButtonCode right, GamepadButtonCode down, GamepadButtonCode left)
        {
            Up = up;
            Right = right;
            Down = down;
            Left = left;
        }

        [field: SerializeField] public GamepadButtonCode Up { get; private set; }
        [field: SerializeField] public GamepadButtonCode Right { get; private set; }
        [field: SerializeField] public GamepadButtonCode Down { get; private set; }
        [field: SerializeField] public GamepadButtonCode Left { get; private set; }

        public override Vector2 Value => value;
        private Vector2 value;

        public override void Update(int index = 0)
        {
            value = Vector2.zero;

            if (TryGetGamepad(index, out Gamepad gamepad))
            {
                if (gamepad[Left].isPressed) value.x++;
                if (gamepad[Right].isPressed) value.x--;
                if (gamepad[Up].isPressed) value.y++;
                if (gamepad[Down].isPressed) value.y--;
            }
        }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "4-dimentional gamepad stick vector input")]
    public sealed class GamepadStickVectorAxis : VectorAxis
    {
        public GamepadStickVectorAxis(GamepadStickCode stick)
        {
            Stick = stick;
        }

        [field: SerializeField] public GamepadStickCode Stick { get; private set; }

        public override Vector2 Value => value;
        private Vector2 value;

        public override void Update(int index = 0)
        {
            if (TryGetGamepad(index, out Gamepad gamepad))
            {
                value = (Stick == GamepadStickCode.RightStick ? gamepad.rightStick : gamepad.leftStick).ReadValue();
                return;
            }

            value = Vector2.zero;
        }
    }

    // User interface input action
    public abstract class InputAction<T>
    {
        public InputAction(T defaultValue = default)
        {
            this.defaultValue = value = defaultValue;
            reactive = new(defaultValue);
        }

        public T Value => value;

        protected T value = default;
        protected readonly AsyncReactiveProperty<T> reactive;
        private readonly T defaultValue;

        public UniTask<T> WaitAsync(CancellationToken cancellationToken = default) => reactive.WaitAsync(cancellationToken);

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

        public static implicit operator T(InputAction<T> action)
        {
            return action.value;
        }
    }

    // Controller base class
    public abstract class Controller : ScriptableObject
    {
        public Vector2 CameraInput { get; protected set; }

        protected sealed class WritableInputAction<T> : InputAction<T>
        {
            public WritableInputAction(T defaultValue = default) : base(defaultValue)
            {

            }

            public new T Value
            {
                get => value;
                set
                {
                    // Only update if the value has changed, this will invoke the reactive
                    if (!EqualityComparer<T>.Default.Equals(this.value, value))
                    {
                        reactive.Value = this.value = value;
                    }
                }
            }
        }

        protected readonly WritableInputAction<bool> confirm = new();
        protected readonly WritableInputAction<bool> cancel = new();
        protected readonly WritableInputAction<MoveDirection> directionInput = new(MoveDirection.None);

        public InputAction<bool> Confirm => confirm;
        public InputAction<bool> Cancel => cancel;
        public InputAction<MoveDirection> DirectionInput => directionInput;


        public abstract void UpdateInput(Player player, bool inputEnabled);

        public virtual bool GetInputGlyph(int index, out Sprite background, out string text)
        {
            background = null;
            text = string.Empty;
            return false;
        }
    }
}

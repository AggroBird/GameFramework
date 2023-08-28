using AggroBird.UnityExtend;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using GamepadButtonCode = UnityEngine.InputSystem.LowLevel.GamepadButton;
using KeyCode = UnityEngine.InputSystem.Key;
using MouseButtonCode = UnityEngine.InputSystem.LowLevel.MouseButton;

namespace AggroBird.GameFramework
{
    public static class InputSystemUtility
    {
        public static StickControl GetStickControl(this Gamepad gamepad, GamepadStick stick)
        {
            return stick == GamepadStick.LeftStick ? gamepad.leftStick : gamepad.rightStick;
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
                _ => mouse.leftButton,
            };
        }
    }

    public enum GamepadStick
    {
        LeftStick = 0,
        RightStick,
    }

    public enum Axis
    {
        Horizontal = 0,
        Vertical,
    }

    public enum ButtonState
    {
        None = 0,
        Pressed,
        Held,
        Released,
    }

    public enum Direction
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
        public abstract Direction Value { get; }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "4-directional keyboard user interface input")]
    public sealed class KeyboardInputDirection : InputDirection
    {
        public KeyboardInputDirection()
        {
            Up = KeyCode.UpArrow;
            Right = KeyCode.RightArrow;
            Down = KeyCode.DownArrow;
            Left = KeyCode.LeftArrow;
        }
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

        public override Direction Value => value;
        private Direction value;

        public override void Update(int index = 0)
        {
            if (TryGetKeyboard(index, out Keyboard keyboard))
            {
                int y = 0;
                if (keyboard[Up].wasPressedThisFrame) y++;
                if (keyboard[Down].wasPressedThisFrame) y--;
                if (y != 0)
                {
                    value = y > 0 ? Direction.Up : Direction.Down;
                    return;
                }

                int x = 0;
                if (keyboard[Right].wasPressedThisFrame) x++;
                if (keyboard[Left].wasPressedThisFrame) x--;
                if (x != 0)
                {
                    value = x > 0 ? Direction.Right : Direction.Left;
                    return;
                }
            }

            value = Direction.None;
        }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "4-directional gamepad user interface input")]
    public sealed class GamepadInputDirection : InputDirection
    {
        public GamepadInputDirection()
        {
            Up = GamepadButtonCode.DpadUp;
            Right = GamepadButtonCode.DpadRight;
            Down = GamepadButtonCode.DpadDown;
            Left = GamepadButtonCode.DpadLeft;
        }
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

        public override Direction Value => value;
        private Direction value;

        public override void Update(int index = 0)
        {
            if (TryGetGamepad(index, out Gamepad gamepad))
            {
                int y = 0;
                if (gamepad[Up].wasPressedThisFrame) y++;
                if (gamepad[Down].wasPressedThisFrame) y--;
                if (y != 0)
                {
                    value = y > 0 ? Direction.Up : Direction.Down;
                    return;
                }

                int x = 0;
                if (gamepad[Right].wasPressedThisFrame) x++;
                if (gamepad[Left].wasPressedThisFrame) x--;
                if (x != 0)
                {
                    value = x > 0 ? Direction.Right : Direction.Left;
                    return;
                }
            }

            value = Direction.None;
        }
    }

    // Linear axes
    [Serializable]
    public abstract class LinearAxis : InputElement
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
    [PolymorphicClassType(Tooltip = "1-dimentional keyboard axis between two keys (range -1 to 1)")]
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
    [PolymorphicClassType(Tooltip = "1-dimentional gamepad axis between two buttons (range -1 to 1)")]
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
    [PolymorphicClassType(Tooltip = "1-dimentional gamepad axis between along single stick axis (range -1 to 1)")]
    public sealed class GamepadStickLinearAxis : LinearAxis
    {
        public GamepadStickLinearAxis(GamepadStick stick, Axis axis)
        {
            Stick = stick;
            Axis = axis;
        }

        [field: SerializeField] public GamepadStick Stick { get; private set; }
        [field: SerializeField] public Axis Axis { get; private set; }

        public override float Value => value;
        private float value;

        public override void Update(int index = 0)
        {
            if (TryGetGamepad(index, out Gamepad gamepad))
            {
                StickControl stick = Stick == GamepadStick.RightStick ? gamepad.rightStick : gamepad.leftStick;
                value = Axis == Axis.Vertical ? stick.ReadValue().y : stick.ReadValue().x;
                return;
            }

            value = 0;
        }
    }

    // Vector axes
    [Serializable]
    public abstract class VectorAxis : InputElement
    {
        public abstract Vector2 Value { get; }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "2-dimentional keyboard vector input")]
    public sealed class KeyboardVectorAxis : VectorAxis
    {
        public KeyboardVectorAxis()
        {
            Up = KeyCode.UpArrow;
            Right = KeyCode.RightArrow;
            Down = KeyCode.DownArrow;
            Left = KeyCode.LeftArrow;
        }
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
                if (keyboard[Up].isPressed) value.y++;
                if (keyboard[Right].isPressed) value.x++;
                if (keyboard[Down].isPressed) value.y--;
                if (keyboard[Left].isPressed) value.x--;
            }
        }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "2-dimentional gamepad D-Pad vector input")]
    public sealed class GamepadDPadVectorAxis : VectorAxis
    {
        public GamepadDPadVectorAxis()
        {
            Up = GamepadButtonCode.DpadUp;
            Right = GamepadButtonCode.DpadRight;
            Down = GamepadButtonCode.DpadDown;
            Left = GamepadButtonCode.DpadLeft;
        }
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
                if (gamepad[Up].isPressed) value.y++;
                if (gamepad[Right].isPressed) value.x++;
                if (gamepad[Left].isPressed) value.x--;
                if (gamepad[Down].isPressed) value.y--;
            }
        }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "2-dimentional gamepad stick vector input")]
    public sealed class GamepadStickVectorAxis : VectorAxis
    {
        public GamepadStickVectorAxis(GamepadStick stick)
        {
            Stick = stick;
        }

        [field: SerializeField] public GamepadStick Stick { get; private set; }

        public override Vector2 Value => value;
        private Vector2 value;

        public override void Update(int index = 0)
        {
            if (TryGetGamepad(index, out Gamepad gamepad))
            {
                value = (Stick == GamepadStick.RightStick ? gamepad.rightStick : gamepad.leftStick).ReadValue();
                return;
            }

            value = Vector2.zero;
        }
    }
}
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

        public static Direction DirectionFromVector(Vector2 vector)
        {
            if (vector.sqrMagnitude > 0.25f)
            {
                return (Direction)(((int)((Mathfx.AngleFromVectorDeg(vector) + 360 + 45) % 360) / 90) & 3) + 1;
            }
            return Direction.None;
        }
        public static Vector2 ToVector2(this Direction direction)
        {
            return direction switch
            {
                Direction.Up => Vector2.up,
                Direction.Right => Vector2.right,
                Direction.Down => Vector2.down,
                Direction.Left => Vector2.left,
                _ => Vector2.zero,
            };
        }
        public static Vector2Int ToVector2Int(this Direction direction)
        {
            return direction switch
            {
                Direction.Up => Vector2Int.up,
                Direction.Right => Vector2Int.right,
                Direction.Down => Vector2Int.down,
                Direction.Left => Vector2Int.left,
                _ => Vector2Int.zero,
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

    public struct ButtonSwitch
    {
        public ButtonState State { get; private set; }

        public static ButtonState UpdateState(ButtonState state, bool isPressed)
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
            return state;
        }
        public static void UpdateState(ref ButtonState state, bool isPressed)
        {
            state = UpdateState(state, isPressed);
        }

        public void Update(bool isPressed)
        {
            State = UpdateState(State, isPressed);
        }

        public static implicit operator ButtonState(ButtonSwitch buttonSwitch)
        {
            return buttonSwitch.State;
        }
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
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "Binary keyboard key switch (true/false)")]
    public sealed class KeyboardKey : InputButton
    {
        public KeyboardKey(KeyCode key)
        {
            this.key = key;
        }

        public KeyCode key;

        public override ButtonState State => state;
        private ButtonState state;

        public override void Update(int index = 0)
        {
            if (key != KeyCode.None && TryGetKeyboard(index, out Keyboard keyboard))
            {
                ButtonSwitch.UpdateState(ref state, keyboard[key].isPressed);
            }
            else
            {
                ButtonSwitch.UpdateState(ref state, false);
            }
        }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "Binary mouse button switch (true/false)")]
    public sealed class MouseButton : InputButton
    {
        public MouseButton(MouseButtonCode button)
        {
            this.button = button;
        }

        public MouseButtonCode button;

        public override ButtonState State => state;
        private ButtonState state;

        public override void Update(int index = 0)
        {
            if (TryGetMouse(index, out Mouse mouse))
            {
                ButtonSwitch.UpdateState(ref state, InputSystemUtility.GetMouseButton(mouse, button).isPressed);
            }
            else
            {
                ButtonSwitch.UpdateState(ref state, false);
            }
        }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "Binary gamepad button switch (true/false)")]
    public sealed class GamepadButton : InputButton
    {
        public GamepadButton(GamepadButtonCode button)
        {
            this.button = button;
        }

        public GamepadButtonCode button;

        public override ButtonState State => state;
        private ButtonState state;

        public override void Update(int index = 0)
        {
            if (TryGetGamepad(index, out Gamepad gamepad))
            {
                ButtonSwitch.UpdateState(ref state, gamepad[button].isPressed);
            }
            else
            {
                ButtonSwitch.UpdateState(ref state, false);
            }
        }
    }

    // UI Directions
    [Serializable]
    public abstract class InputDirection : InputElement
    {
        public bool repeat;
        [ConditionalField(nameof(repeat), ConditionalFieldOperator.Equal, true), Min(0)]
        public float repeatDelay = 0.3f;
        [ConditionalField(nameof(repeat), ConditionalFieldOperator.Equal, true), Min(0)]
        public float repeatInterval = 0.1f;

        public abstract Direction Value { get; }
        private Direction lastValue = Direction.None;
        private double inputTime;
        private int inputIndex;

        protected Direction ApplyRepeat(Direction current)
        {
            if (repeat)
            {
                if (current != Direction.None)
                {
                    if (current != lastValue)
                    {
                        lastValue = current;
                        inputTime = Time.unscaledTimeAsDouble;
                        inputIndex = -1;
                        return current;
                    }
                    else if (repeatInterval > 0)
                    {
                        double t = Time.unscaledTimeAsDouble - inputTime;
                        if (t >= repeatDelay)
                        {
                            int idx = (int)((t - repeatDelay) / repeatInterval);
                            if (inputIndex != idx)
                            {
                                inputIndex = idx;
                                return current;
                            }
                        }
                    }
                }
                else
                {
                    lastValue = Direction.None;
                }
            }
            else
            {
                if (current != lastValue)
                {
                    lastValue = current;
                    return current;
                }
            }

            return Direction.None;
        }
        protected Direction MakeDirection(int x, int y)
        {
            return x > 0 ? Direction.Right : x < 0 ? Direction.Left : y > 0 ? Direction.Up : y < 0 ? Direction.Down : Direction.None;
        }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "4-directional keyboard user interface input")]
    public sealed class KeyboardInputDirection : InputDirection
    {
        public KeyboardInputDirection()
        {
            up = KeyCode.UpArrow;
            right = KeyCode.RightArrow;
            down = KeyCode.DownArrow;
            left = KeyCode.LeftArrow;
        }
        public KeyboardInputDirection(KeyCode up, KeyCode right, KeyCode down, KeyCode left)
        {
            this.up = up;
            this.right = right;
            this.down = down;
            this.left = left;
        }

        public KeyCode up;
        public KeyCode right;
        public KeyCode down;
        public KeyCode left;

        public override Direction Value => value;
        private Direction value;

        public override void Update(int index = 0)
        {
            if (TryGetKeyboard(index, out Keyboard keyboard))
            {
                int y = 0;
                if (keyboard[up].isPressed) y++;
                if (keyboard[down].isPressed) y--;
                int x = 0;
                if (keyboard[right].isPressed) x++;
                if (keyboard[left].isPressed) x--;
                value = ApplyRepeat(MakeDirection(x, y));
                return;
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
            up = GamepadButtonCode.DpadUp;
            right = GamepadButtonCode.DpadRight;
            down = GamepadButtonCode.DpadDown;
            left = GamepadButtonCode.DpadLeft;
        }
        public GamepadInputDirection(GamepadButtonCode up, GamepadButtonCode right, GamepadButtonCode down, GamepadButtonCode left)
        {
            this.up = up;
            this.right = right;
            this.down = down;
            this.left = left;
        }

        public GamepadButtonCode up;
        public GamepadButtonCode right;
        public GamepadButtonCode down;
        public GamepadButtonCode left;

        public override Direction Value => value;
        private Direction value;

        public override void Update(int index = 0)
        {
            if (TryGetGamepad(index, out Gamepad gamepad))
            {
                int y = 0;
                if (gamepad[up].isPressed) y++;
                if (gamepad[down].isPressed) y--;
                int x = 0;
                if (gamepad[right].isPressed) x++;
                if (gamepad[left].isPressed) x--;
                value = value = ApplyRepeat(MakeDirection(x, y));
                return;
            }

            value = Direction.None;
        }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "4-directional gamepad stick user interface input")]
    public sealed class GamepadStickInputDirection : InputDirection
    {
        public GamepadStick stick;

        public override Direction Value => value;
        private Direction value;

        public override void Update(int index = 0)
        {
            if (TryGetGamepad(index, out Gamepad gamepad))
            {
                value = ApplyRepeat(InputSystemUtility.DirectionFromVector(gamepad.GetStickControl(stick).ReadValue()));
                return;
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
            this.key = key;
        }

        public KeyCode key;

        public override float Value => value;
        private float value;

        public override void Update(int index = 0)
        {
            if (TryGetKeyboard(index, out Keyboard keyboard))
            {
                value = keyboard[key].ReadValue();
            }
            else
            {
                value = 0;
            }
        }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "Mouse single button axis (range 0 to 1)")]
    public sealed class MouseAxisButton : LinearAxis
    {
        public MouseAxisButton(MouseButtonCode button)
        {
            this.button = button;
        }

        public MouseButtonCode button;

        public override float Value => value;
        private float value;

        public override void Update(int index = 0)
        {
            if (TryGetMouse(index, out Mouse mouse))
            {
                value = InputSystemUtility.GetMouseButton(mouse, button).ReadValue();
            }
            else
            {
                value = 0;
            }
        }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "Gamepad single button axis (range 0 to 1)")]
    public sealed class GamepadAxisButton : LinearAxis
    {
        public GamepadAxisButton(GamepadButtonCode button)
        {
            this.button = button;
        }

        public GamepadButtonCode button;

        public override float Value => value;
        private float value;

        public override void Update(int index = 0)
        {
            if (TryGetGamepad(index, out Gamepad gamepad))
            {
                value = gamepad[button].ReadValue();
            }
            else
            {
                value = 0;
            }
        }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "1-dimentional keyboard axis between two keys (range -1 to 1)")]
    public sealed class KeyboardKeyLinearAxis : LinearAxis
    {
        public KeyboardKeyLinearAxis(KeyCode positive, KeyCode negative)
        {
            this.positive = positive;
            this.negative = negative;
        }

        public KeyCode positive;
        public KeyCode negative;

        public override float Value => value;
        private float value;

        public override void Update(int index = 0)
        {
            if (TryGetKeyboard(index, out Keyboard keyboard))
            {
                value = keyboard[positive].ReadValue() - keyboard[negative].ReadValue();
            }
            else
            {
                value = 0;
            }
        }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "1-dimentional gamepad axis between two buttons (range -1 to 1)")]
    public sealed class GamepadButtonLinearAxis : LinearAxis
    {
        public GamepadButtonLinearAxis(GamepadButtonCode positive, GamepadButtonCode negative)
        {
            this.positive = positive;
            this.negative = negative;
        }

        public GamepadButtonCode positive;
        public GamepadButtonCode negative;

        public override float Value => value;
        private float value;

        public override void Update(int index = 0)
        {
            if (TryGetGamepad(index, out Gamepad gamepad))
            {
                value = gamepad[positive].ReadValue() - gamepad[negative].ReadValue();
            }
            else
            {
                value = 0;
            }
        }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "1-dimentional gamepad axis between along single stick axis (range -1 to 1)")]
    public sealed class GamepadStickLinearAxis : LinearAxis
    {
        public GamepadStickLinearAxis(GamepadStick stick, Axis axis)
        {
            this.stick = stick;
            this.axis = axis;
        }

        public GamepadStick stick;
        public Axis axis;
        public bool invert;

        public bool applyCurve;
        [ConditionalField(nameof(applyCurve), ConditionalFieldOperator.Equal, true)]
        public AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);

        public override float Value => value;
        private float value;

        public override void Update(int index = 0)
        {
            if (TryGetGamepad(index, out Gamepad gamepad))
            {
                StickControl stickControl = stick == GamepadStick.RightStick ? gamepad.rightStick : gamepad.leftStick;
                value = axis == Axis.Vertical ? stickControl.ReadValue().y : stickControl.ReadValue().x;
            }
            else
            {
                value = 0;
            }

            if (applyCurve && curve != null)
            {
                value = Mathf.Sign(value) * curve.Evaluate(Mathf.Abs(value));
            }

            if (invert) value = -value;
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
            up = KeyCode.UpArrow;
            right = KeyCode.RightArrow;
            down = KeyCode.DownArrow;
            left = KeyCode.LeftArrow;
        }
        public KeyboardVectorAxis(KeyCode up, KeyCode right, KeyCode down, KeyCode left)
        {
            this.up = up;
            this.right = right;
            this.down = down;
            this.left = left;
        }

        public KeyCode up;
        public KeyCode right;
        public KeyCode down;
        public KeyCode left;
        public bool normalize = true;

        public override Vector2 Value => value;
        private Vector2 value;

        public override void Update(int index = 0)
        {
            value = Vector2.zero;

            if (TryGetKeyboard(index, out Keyboard keyboard))
            {
                if (keyboard[up].isPressed) value.y++;
                if (keyboard[right].isPressed) value.x++;
                if (keyboard[down].isPressed) value.y--;
                if (keyboard[left].isPressed) value.x--;

                if (normalize) value.Normalize();
            }
        }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "2-dimentional mouse delta input")]
    public sealed class MouseDeltaVectorAxis : VectorAxis
    {
        public float sensitivity = 1;
        public bool invertHorizontal;
        public bool invertVertical;

        public override Vector2 Value => value;
        private Vector2 value;

        public override void Update(int index = 0)
        {
            if (TryGetMouse(index, out var mouse))
            {
                value = mouse.delta.ReadValue() * sensitivity;

                if (invertHorizontal) value.x = -value.x;
                if (invertVertical) value.y = -value.y;
            }
            else
            {
                value = Vector2.zero;
            }
        }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "2-dimentional gamepad D-Pad vector input")]
    public sealed class GamepadDPadVectorAxis : VectorAxis
    {
        public GamepadDPadVectorAxis()
        {
            up = GamepadButtonCode.DpadUp;
            right = GamepadButtonCode.DpadRight;
            down = GamepadButtonCode.DpadDown;
            left = GamepadButtonCode.DpadLeft;
        }
        public GamepadDPadVectorAxis(GamepadButtonCode up, GamepadButtonCode right, GamepadButtonCode down, GamepadButtonCode left)
        {
            this.up = up;
            this.right = right;
            this.down = down;
            this.left = left;
        }

        public GamepadButtonCode up;
        public GamepadButtonCode right;
        public GamepadButtonCode down;
        public GamepadButtonCode left;
        public bool normalize = true;

        public override Vector2 Value => value;
        private Vector2 value;

        public override void Update(int index = 0)
        {
            value = Vector2.zero;

            if (TryGetGamepad(index, out Gamepad gamepad))
            {
                if (gamepad[up].isPressed) value.y++;
                if (gamepad[right].isPressed) value.x++;
                if (gamepad[down].isPressed) value.y--;
                if (gamepad[left].isPressed) value.x--;

                if (normalize) value.Normalize();
            }
        }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "2-dimentional gamepad stick vector input")]
    public sealed class GamepadStickVectorAxis : VectorAxis
    {
        public GamepadStickVectorAxis(GamepadStick stick)
        {
            this.stick = stick;
        }

        public GamepadStick stick;
        public bool invertHorizontal;
        public bool invertVertical;

        public bool applyCurve;
        [ConditionalField(nameof(applyCurve), ConditionalFieldOperator.Equal, true)]
        public AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);

        public override Vector2 Value => value;
        private Vector2 value;

        public override void Update(int index = 0)
        {
            value = Vector2.zero;

            if (TryGetGamepad(index, out Gamepad gamepad))
            {
                value = (stick == GamepadStick.RightStick ? gamepad.rightStick : gamepad.leftStick).ReadValue();
            }

            if (applyCurve && curve != null)
            {
                value = value.normalized * curve.Evaluate(value.magnitude);
            }

            if (invertHorizontal) value.x = -value.x;
            if (invertVertical) value.y = -value.y;
        }
    }
}
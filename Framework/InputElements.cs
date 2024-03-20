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

        public static Direction DirectionFromVector(Vector2 vector, float sensitivity = 0.5f)
        {
            if (vector.sqrMagnitude > sensitivity)
            {
                return DirectionFromAngle(Mathfx.AngleFromVectorDeg(vector));
            }
            return Direction.None;
        }
        public static Direction DirectionFromAngle(float angle)
        {
            return (Direction)(((int)((Mathfx.ModAbs(angle, 360) + 45) % 360) / 90) & 3) + 1;
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

        public static Direction MakeDirection(int x, int y)
        {
            return x > 0 ? Direction.Right : x < 0 ? Direction.Left : y > 0 ? Direction.Up : y < 0 ? Direction.Down : Direction.None;
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

    [Serializable]
    public struct ButtonSwitch
    {
        public static ButtonSwitch Default => new(false);

        public ButtonSwitch(bool repeat, float repeatDelay = 0.3f, float repeatInterval = 0.1f)
        {
            this.repeat = repeat;
            this.repeatDelay = repeatDelay;
            this.repeatInterval = repeatInterval;

            State = ButtonState.None;
            lastValue = false;
            inputTime = 0;
            inputIndex = 0;
        }

        public bool repeat;
        [ConditionalField(nameof(repeat), ConditionalFieldOperator.Equal, true), Min(0)]
        public float repeatDelay;
        [ConditionalField(nameof(repeat), ConditionalFieldOperator.Equal, true), Min(0)]
        public float repeatInterval;

        public ButtonState State { get; private set; }

        private bool lastValue;
        private double inputTime;
        private int inputIndex;

        public void Update(bool isPressed)
        {
            if (repeat)
            {
                if (isPressed)
                {
                    if (!lastValue)
                    {
                        // First press
                        lastValue = isPressed;
                        inputTime = Time.unscaledTimeAsDouble;
                        inputIndex = -1;
                        State = ButtonState.Pressed;
                        return;
                    }
                    else if (repeatInterval > 0)
                    {
                        // Repeating interval presses
                        double t = Time.unscaledTimeAsDouble - inputTime;
                        if (t >= repeatDelay)
                        {
                            int idx = (int)((t - repeatDelay) / repeatInterval);
                            if (inputIndex != idx)
                            {
                                inputIndex = idx;
                                State = ButtonState.Pressed;
                                return;
                            }
                        }
                    }
                }
                else
                {
                    // Release
                    lastValue = false;
                }
            }

            State = InputSystemUtility.UpdateState(State, isPressed);
        }

        public static implicit operator ButtonState(ButtonSwitch buttonSwitch)
        {
            return buttonSwitch.State;
        }
    }

    [Serializable]
    public struct DirectionSwitch
    {
        public static DirectionSwitch Default => new(false);

        public DirectionSwitch(bool repeat, float repeatDelay = 0.3f, float repeatInterval = 0.1f)
        {
            this.repeat = repeat;
            this.repeatDelay = repeatDelay;
            this.repeatInterval = repeatInterval;

            lastValue = State = Direction.None;
            inputTime = 0;
            inputIndex = 0;
        }

        public bool repeat;
        [ConditionalField(nameof(repeat), ConditionalFieldOperator.Equal, true), Min(0)]
        public float repeatDelay;
        [ConditionalField(nameof(repeat), ConditionalFieldOperator.Equal, true), Min(0)]
        public float repeatInterval;

        public Direction State { get; private set; }

        private Direction lastValue;
        private double inputTime;
        private int inputIndex;

        public void Update(Direction direction)
        {
            if (repeat)
            {
                if (direction != Direction.None)
                {
                    if (direction != lastValue)
                    {
                        // First press
                        lastValue = direction;
                        inputTime = Time.unscaledTimeAsDouble;
                        inputIndex = -1;
                        State = direction;
                        return;
                    }
                    else if (repeatInterval > 0)
                    {
                        // Repeating interval presses
                        double t = Time.unscaledTimeAsDouble - inputTime;
                        if (t >= repeatDelay)
                        {
                            int idx = (int)((t - repeatDelay) / repeatInterval);
                            if (inputIndex != idx)
                            {
                                inputIndex = idx;
                                State = direction;
                                return;
                            }
                        }
                    }
                }
                else
                {
                    // Release
                    lastValue = Direction.None;
                }
            }
            else
            {
                // Non-repeating, only change once per frame
                if (direction != lastValue)
                {
                    lastValue = direction;
                    State = direction;
                    return;
                }
            }

            State = Direction.None;
        }

        public static implicit operator Direction(DirectionSwitch directionSwitch)
        {
            return directionSwitch.State;
        }
    }

    // Input elements
    [Serializable]
    public abstract class InputElement
    {
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
        public abstract bool GetValue(int index = 0);
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

        public override bool GetValue(int index = 0)
        {
            return key != KeyCode.None && TryGetKeyboard(index, out Keyboard keyboard) && keyboard[key].isPressed;
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

        public override bool GetValue(int index = 0)
        {
            return TryGetMouse(index, out Mouse mouse) && InputSystemUtility.GetMouseButton(mouse, button).isPressed;
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

        public override bool GetValue(int index = 0)
        {
            return TryGetGamepad(index, out Gamepad gamepad) && gamepad[button].isPressed;
        }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "Gamepad stick direction button (true/false)")]
    public sealed class GamepadStickDirectionButton : InputButton
    {
        public GamepadStickDirectionButton(GamepadStick stick, Direction direction, float sensitivity = 0.5f)
        {
            this.stick = stick;
            this.direction = direction;
            this.sensitivity = sensitivity;
        }

        public GamepadStick stick;
        public Direction direction;
        [Clamped(0, 1)]
        public float sensitivity = 0.5f;

        public override bool GetValue(int index = 0)
        {
            if (direction != Direction.None && TryGetGamepad(index, out Gamepad gamepad))
            {
                return InputSystemUtility.DirectionFromVector(gamepad.GetStickControl(stick).ReadValue(), sensitivity) == direction;
            }

            return false;
        }
    }

    // UI Directions
    [Serializable]
    public abstract class InputDirection : InputElement
    {
        public abstract Direction GetValue(int index = 0);
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

        public override Direction GetValue(int index = 0)
        {
            if (TryGetKeyboard(index, out Keyboard keyboard))
            {
                int y = 0;
                if (keyboard[up].isPressed) y++;
                if (keyboard[down].isPressed) y--;
                int x = 0;
                if (keyboard[right].isPressed) x++;
                if (keyboard[left].isPressed) x--;
                return InputSystemUtility.MakeDirection(x, y);
            }

            return Direction.None;
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

        public override Direction GetValue(int index = 0)
        {
            if (TryGetGamepad(index, out Gamepad gamepad))
            {
                int y = 0;
                if (gamepad[up].isPressed) y++;
                if (gamepad[down].isPressed) y--;
                int x = 0;
                if (gamepad[right].isPressed) x++;
                if (gamepad[left].isPressed) x--;
                return InputSystemUtility.MakeDirection(x, y);
            }

            return Direction.None;
        }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "4-directional gamepad stick user interface input")]
    public sealed class GamepadStickInputDirection : InputDirection
    {
        public GamepadStickInputDirection(GamepadStick stick, float sensitivity = 0.5f)
        {
            this.stick = stick;
            this.sensitivity = sensitivity;
        }

        public GamepadStick stick;
        [Clamped(0, 1)]
        public float sensitivity = 0.5f;

        public override Direction GetValue(int index = 0)
        {
            if (TryGetGamepad(index, out Gamepad gamepad))
            {
                return InputSystemUtility.DirectionFromVector(gamepad.GetStickControl(stick).ReadValue());
            }
            else
            {
                return Direction.None;
            }
        }
    }

    // Linear axes
    [Serializable]
    public abstract class LinearAxis : InputElement
    {
        public abstract float GetValue(int index = 0);
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

        public override float GetValue(int index = 0)
        {
            return TryGetKeyboard(index, out Keyboard keyboard) ? keyboard[key].ReadValue() : 0;
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

        public override float GetValue(int index = 0)
        {
            return TryGetMouse(index, out Mouse mouse) ? mouse.GetMouseButton(button).ReadValue() : 0;
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

        public override float GetValue(int index = 0)
        {
            return TryGetGamepad(index, out Gamepad gamepad) ? gamepad[button].ReadValue() : 0;
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

        public override float GetValue(int index = 0)
        {
            return TryGetKeyboard(index, out Keyboard keyboard) ? (keyboard[positive].ReadValue() - keyboard[negative].ReadValue()) : 0;
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

        public override float GetValue(int index = 0)
        {
            return TryGetGamepad(index, out Gamepad gamepad) ? (gamepad[positive].ReadValue() - gamepad[negative].ReadValue()) : 0;
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

        public override float GetValue(int index = 0)
        {
            float value;

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

            return value;
        }
    }

    // Vector axes
    [Serializable]
    public abstract class VectorAxis : InputElement
    {
        public abstract Vector2 GetValue(int index = 0);
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

        public override Vector2 GetValue(int index = 0)
        {
            Vector2 value = Vector2.zero;

            if (TryGetKeyboard(index, out Keyboard keyboard))
            {
                if (keyboard[up].isPressed) value.y++;
                if (keyboard[right].isPressed) value.x++;
                if (keyboard[down].isPressed) value.y--;
                if (keyboard[left].isPressed) value.x--;

                if (normalize) value.Normalize();
            }

            return value;
        }
    }

    [Serializable]
    [PolymorphicClassType(Tooltip = "2-dimentional mouse delta input")]
    public sealed class MouseDeltaVectorAxis : VectorAxis
    {
        public float sensitivity = 1;
        public bool invertHorizontal;
        public bool invertVertical;

        public override Vector2 GetValue(int index = 0)
        {
            if (TryGetMouse(index, out var mouse))
            {
                Vector2 value = mouse.delta.ReadValue() * sensitivity;

                if (invertHorizontal) value.x = -value.x;
                if (invertVertical) value.y = -value.y;

                return value;
            }
            else
            {
                return Vector2.zero;
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

        public override Vector2 GetValue(int index = 0)
        {
            if (TryGetGamepad(index, out Gamepad gamepad))
            {
                Vector2 value = Vector2.zero;

                if (gamepad[up].isPressed) value.y++;
                if (gamepad[right].isPressed) value.x++;
                if (gamepad[down].isPressed) value.y--;
                if (gamepad[left].isPressed) value.x--;

                if (normalize) value.Normalize();

                return value;
            }
            else
            {
                return Vector2.zero;
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

        public override Vector2 GetValue(int index = 0)
        {
            Vector2 value = Vector2.zero;

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

            return value;
        }
    }
}
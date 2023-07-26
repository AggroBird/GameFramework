using AggroBird.UnityExtend;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace AggroBird.GameFramework
{
    // Simple controller for standalone applications (with keyboard and mouse)
    internal class StandaloneController : Controller
    {
        private static readonly Key[] KeyboardDirections =
        {
            Key.LeftArrow,
            Key.UpArrow,
            Key.RightArrow,
            Key.DownArrow,
        };
        private static readonly GamepadButton[] DpadDirections =
        {
            GamepadButton.DpadLeft,
            GamepadButton.DpadUp,
            GamepadButton.DpadRight,
            GamepadButton.DpadDown,
        };
        private bool hasDirectionInput = false;
        private double directionInputTime;
        private int directionInputIndex;

        public override void UpdateInput(Player player, bool inputEnabled)
        {
            bool confirm = false;
            bool cancel = false;
            MoveDirection activeDirection = MoveDirection.None;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                confirm |= keyboard[Key.Enter].wasPressedThisFrame;
                cancel |= keyboard[Key.Escape].wasPressedThisFrame;

                for (int i = 0; i < 4; i++)
                {
                    if (keyboard[KeyboardDirections[i]].isPressed)
                    {
                        activeDirection = (MoveDirection)i;
                        break;
                    }
                }
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                confirm |= gamepad[GamepadButton.A].wasPressedThisFrame;
                cancel |= gamepad[GamepadButton.B].wasPressedThisFrame;

                if (activeDirection == MoveDirection.None)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (gamepad[DpadDirections[i]].isPressed)
                        {
                            activeDirection = (MoveDirection)i;
                            break;
                        }
                    }
                    if (activeDirection == MoveDirection.None)
                    {
                        Vector2 stickInput = gamepad.leftStick.ReadValue();
                        if (stickInput.magnitude > 0.75f)
                        {
                            activeDirection = (MoveDirection)(((int)((Mathfx.AngleFromVectorDeg(stickInput) + 495) % 360) / 90) & 3);
                        }
                    }
                }
            }

            MoveDirection direction = MoveDirection.None;
            if (activeDirection != MoveDirection.None)
            {
                if (!hasDirectionInput)
                {
                    hasDirectionInput = true;
                    directionInputIndex = -1;
                    directionInputTime = Time.unscaledTimeAsDouble;
                    direction = activeDirection;
                }
                else
                {
                    double dif = Time.unscaledTimeAsDouble - directionInputTime;
                    if (dif >= 0.3)
                    {
                        int index = (int)((dif - 0.3) / 0.1);
                        if (index != directionInputIndex)
                        {
                            directionInputIndex = index;
                            direction = activeDirection;
                        }
                    }
                }
            }
            else
            {
                hasDirectionInput = false;
            }

            this.confirm.Value = confirm;
            this.cancel.Value = cancel;
            directionInput.Value = direction;
        }
    }
}
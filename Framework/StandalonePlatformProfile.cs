using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace AggroBird.GameFramework
{
    // Simple platform profile for standalone applications (with keyboard and mouse)
    internal class StandalonePlatformProfile : PlatformProfile
    {
        private InputMode inputMode;

        public override bool SupportsMouseKeyboard => true;
        public override InputMode ActiveInputMode => inputMode;


        public override void Initialize()
        {
            inputMode = Keyboard.current != null && SupportsMouseKeyboard ? InputMode.KeyboardMouse : InputMode.Controller;
        }

        public override void UpdateInputMode()
        {
            if (inputMode == InputMode.KeyboardMouse)
            {
                Gamepad gamepad = Gamepad.current;
                if (gamepad != null)
                {
                    foreach (var control in Gamepad.current.allControls)
                    {
                        if (!control.synthetic && control is ButtonControl button && button.wasPressedThisFrame)
                        {
                            SwitchInputMode(InputMode.Controller);
                            return;
                        }
                    }

                    if (gamepad.leftStick.ReadValue().magnitude > 0.1f || gamepad.rightStick.ReadValue().magnitude > 0.1f)
                    {
                        SwitchInputMode(InputMode.Controller);
                        return;
                    }
                }
            }
            else if (SupportsMouseKeyboard)
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.anyKey.wasPressedThisFrame)
                    {
                        SwitchInputMode(InputMode.KeyboardMouse);
                        return;
                    }
                }

                Mouse mouse = Mouse.current;
                if (mouse != null)
                {
                    if (mouse.delta.ReadValue().magnitude > 0.1f)
                    {
                        SwitchInputMode(InputMode.KeyboardMouse);
                        return;
                    }

                    if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
                    {
                        SwitchInputMode(InputMode.KeyboardMouse);
                        return;
                    }
                }
            }
        }

        public override Controller CreateController(int player)
        {
            return CreateInstance<StandaloneController>();
        }

        private void SwitchInputMode(InputMode newMode)
        {
            if (inputMode != newMode)
            {
                inputMode = newMode;
                InputModeChanged(inputMode);
            }
        }
    }
}
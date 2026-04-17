namespace AggroBird.GameFramework
{
    // Simple controller for standalone applications (with keyboard and mouse)
    internal sealed class StandaloneController : Controller
    {
        private void OnEnable()
        {
            Default = new()
            {
                CameraInput = new VectorAxisMapping(new MouseDeltaVectorAxis()),
                Confirm = new InputButtonMapping(new InputButton[] { new MouseButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left) }),
                Cancel = new InputButtonMapping(new InputButton[] { new MouseButton(UnityEngine.InputSystem.LowLevel.MouseButton.Right) }),
                DirectionInput = new InputDirectionMapping(new KeyboardInputDirection()),
            };
        }
    }
}
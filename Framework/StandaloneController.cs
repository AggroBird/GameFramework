
namespace AggroBird.GameFramework
{
    // Simple controller for standalone applications (with keyboard and mouse)
    internal sealed class StandaloneController : Controller
    {
        private void OnEnable()
        {
            CameraInput = new VectorAxisMapping(new MouseDeltaVectorAxis());
            Confirm = new InputButtonMapping(new InputButton[] { new MouseButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left) });
            Cancel = new InputButtonMapping(new InputButton[] { new MouseButton(UnityEngine.InputSystem.LowLevel.MouseButton.Right) });
            DirectionInput = new InputDirectionMapping(new KeyboardInputDirection());
        }

        protected internal override void UpdateInput(Player player, int index, bool inputEnabled)
        {
            CameraInput.Update(index);
            Confirm.Update(index);
            Cancel.Update(index);
            DirectionInput.Update(index);
        }
    }
}
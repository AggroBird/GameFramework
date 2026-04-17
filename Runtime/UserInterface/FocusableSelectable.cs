using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AggroBird.GameFramework
{
    public abstract class FocusableSelectable : Button
    {
        protected internal virtual void OnGainFocus()
        {

        }
        protected internal virtual void OnLoseFocus()
        {

        }
        protected internal virtual void OnDirectionInput(Direction direction)
        {

        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            base.OnPointerClick(eventData);

            if (eventData.button == PointerEventData.InputButton.Left)
            {
                IMenu menu = gameObject.GetComponentInParent<IMenu>();
                if (menu != null)
                {
                    var parent = menu.Parent;
                    if (parent)
                    {
                        parent.FocusSelectable(this);
                    }
                }
            }
        }
    }
}
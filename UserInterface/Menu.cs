using UnityEngine;
using UnityEngine.UI;

namespace AggroBird.GameFramework
{
    public abstract class Menu : Widget
    {
        protected virtual Selectable SelectOnFocus => null;

        [field: SerializeField]
        public bool AllowInput { get; protected set; }
        [field: SerializeField]
        public bool PauseGame { get; private set; }

        public UserInterface Parent { get; internal set; }
        public bool IsTop => Parent ? ReferenceEquals(Parent.Top, this) : false;


        protected override void OnClosed()
        {
            base.OnClosed();

            if (Parent)
            {
                Parent.RemoveFromStack(this);
                Parent = null;
            }

            Destroy(gameObject);
        }


        public void FocusSelectable()
        {
            if (Parent)
            {
                Selectable selectOnFocus = SelectOnFocus;
                if (selectOnFocus)
                {
                    Parent.SetSelection(selectOnFocus);
                }
                else
                {
                    Selectable[] selectables = GetComponentsInChildren<Selectable>();
                    foreach (var selectable in selectables)
                    {
                        if (selectable.interactable && selectable.gameObject.activeInHierarchy)
                        {
                            Parent.SetSelection(selectable);
                            break;
                        }
                    }
                }
            }
        }

        protected override void OnOpened()
        {
            base.OnOpened();

            if (AppInstance.Instance.PlatformProfile.ActiveInputMode == InputMode.Gamepad)
            {
                FocusSelectable();
            }
        }


        public virtual void UpdateWidget(bool isTop)
        {

        }


        public virtual bool OnConfirm()
        {
            if (Parent)
            {
                Parent.HandleConfirm();
            }

            return true;
        }
        public virtual bool OnCancel()
        {
            Close();

            return true;
        }
        public virtual bool OnDirection(Direction direction)
        {
            if (Parent)
            {
                Parent.HandleDirectionInput(direction);
            }

            return true;
        }
    }
}
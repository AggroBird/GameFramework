using UnityEngine;
using UnityEngine.UI;

namespace AggroBird.GameFramework
{
    public abstract class Menu : Widget, IMenu
    {
        protected virtual Selectable SelectOnFocus => null;
        protected virtual bool ForceSelectOnFocus => false;

        [field: SerializeField]
        public bool AllowInput { get; protected set; }
        [field: SerializeField]
        public bool ConsumeEvents { get; protected set; } = true;
        [field: SerializeField]
        public bool PauseGame { get; private set; }
        [field: SerializeField]
        public bool AspectSensitive { get; private set; }

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


        public bool FocusSelectable()
        {
            if (Parent)
            {
                Selectable selectOnFocus = SelectOnFocus;
                if (selectOnFocus)
                {
                    Parent.SetSelection(selectOnFocus);
                    return true;
                }
                else
                {
                    Selectable[] selectables = GetComponentsInChildren<Selectable>();
                    foreach (var selectable in selectables)
                    {
                        if (selectable.interactable && selectable.gameObject.activeInHierarchy)
                        {
                            Parent.SetSelection(selectable);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        protected override void OnOpened()
        {
            base.OnOpened();

            bool FocusSelectableOnOpen()
            {
                if (ForceSelectOnFocus)
                {
                    return true;
                }
                else if (AppInstance.TryGetInstance(out AppInstance instance))
                {
                    var platformProfile = instance.PlatformProfile;
                    if (platformProfile && platformProfile.ActiveInputMode == InputMode.Gamepad)
                    {
                        return true;
                    }
                }
                return false;
            }

            if (FocusSelectableOnOpen())
            {
                FocusSelectable();
            }
        }


        public virtual void UpdateWidget(bool isTop)
        {

        }


        public virtual bool OnConfirm()
        {
            if (Parent && Parent.HandleConfirm())
            {

            }

            return true;
        }
        public virtual bool OnCancel()
        {
            if (Parent && Parent.HandleCancel())
            {

            }

            Close();
            return true;
        }
        public virtual bool OnDirection(Direction direction)
        {
            if (Parent && Parent.HandleDirectionInput(direction))
            {

            }

            return true;
        }
    }
}
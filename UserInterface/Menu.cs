using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AggroBird.GameFramework
{
    public abstract class Menu : Widget
    {
        protected virtual Selectable SelectOnFocus => null;

        [field: SerializeField]
        public bool AllowInput { get; private set; }
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

            if (AppInstance.Instance.PlatformProfile.ActiveInputMode == InputMode.Controller)
            {
                FocusSelectable();
            }
        }


        public virtual void UpdateWidget(bool isTop)
        {

        }


        public virtual bool OnConfirm()
        {
            // Press current selection if controller
            if (AppInstance.Instance.PlatformProfile.ActiveInputMode == InputMode.Controller && Parent)
            {
                EventSystem eventSystem = Parent.EventSystem;
                if (eventSystem)
                {
                    GameObject selectedGameobject = eventSystem.currentSelectedGameObject;
                    if (selectedGameobject && selectedGameobject.GetComponentInParent<Menu>() == this)
                    {
                        PointerEventData data = new(EventSystem.current);
                        ExecuteEvents.Execute(selectedGameobject, data, ExecuteEvents.pointerClickHandler);
                    }
                }
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
                EventSystem eventSystem = Parent.EventSystem;
                if (eventSystem)
                {
                    // Send move event
                    GameObject selectedGameobject = eventSystem.currentSelectedGameObject;
                    if (selectedGameobject && selectedGameobject.GetComponentInParent<Menu>() == this)
                    {
                        AxisEventData data = new(Parent.EventSystem)
                        {
                            moveDir = direction switch
                            {
                                Direction.Up => MoveDirection.Up,
                                Direction.Right => MoveDirection.Right,
                                Direction.Down => MoveDirection.Down,
                                Direction.Left => MoveDirection.Left,
                                _ => MoveDirection.None,
                            }
                        };
                        ExecuteEvents.Execute(selectedGameobject, data, ExecuteEvents.moveHandler);
                    }
                }
            }

            return true;
        }
    }
}
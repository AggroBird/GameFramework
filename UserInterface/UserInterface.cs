using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AggroBird.GameFramework
{
    public abstract class UserInterface : MonoBehaviour
    {
        public Player Owner { get; private set; }

        public abstract RectTransform RootTransform { get; }
        public abstract EventSystem EventSystem { get; }


        private List<Menu> stack = new List<Menu>();
        public int StackCount => stack.Count;
        public Menu Top => stack.Count > 0 ? stack[^1] : null;

        public bool AllowInput { get; private set; }
        public bool PauseGame { get; private set; }

        private GameObject currentSelection;
        private ScrollRect scrollingScrollRect;
        private float scrollRectOriginValue;
        private float scrollRectTargetValue;
        private float scrollRectProgress;
        private void ScrollTo(ScrollRect scrollRect, float target)
        {
            scrollingScrollRect = scrollRect;
            scrollRectOriginValue = scrollingScrollRect.verticalNormalizedPosition;
            scrollRectTargetValue = target;
            scrollRectProgress = 0;
        }
        private bool delayScrollLayout = false;
        public void RefreshScrollLayout()
        {
            delayScrollLayout = true;
        }

        private readonly Vector3[] Corners = new Vector3[4];
        private Rect GetRectFromCorners()
        {
            return new Rect(Corners[0].x, Corners[0].y, Corners[2].x - Corners[0].x, Corners[2].y - Corners[0].y);
        }
        private Rect GetRect(RectTransform rectTransform)
        {
            rectTransform.GetWorldCorners(Corners);
            return GetRectFromCorners();
        }


        public virtual void Initialize(Player Owner)
        {
            if (AppInstance.IsInitialized)
            {
                AppInstance.Instance.PlatformProfile.OnInputModeChanged += OnInputModeChanged;
            }

            this.Owner = Owner;
        }
        protected virtual void OnDestroy()
        {
            if (AppInstance.IsInitialized)
            {
                AppInstance.Instance.PlatformProfile.OnInputModeChanged -= OnInputModeChanged;
            }
        }


        public void SetSelection(Selectable selectable)
        {
            EventSystem.SetSelectedGameObject(selectable ? selectable.gameObject : null);
        }

        public static void LinkElementsHorizontal(IReadOnlyList<Selectable> elements)
        {
            if (elements.Count > 0)
            {
                if (elements.Count == 1)
                {
                    elements[0].navigation = new Navigation
                    {
                        mode = Navigation.Mode.Explicit,
                    };
                }
                else
                {
                    Selectable prev = null;
                    for (int i = 0; i < elements.Count - 1; i++)
                    {
                        elements[i].navigation = new Navigation
                        {
                            mode = Navigation.Mode.Explicit,
                            selectOnLeft = prev,
                            selectOnRight = elements[i + 1],
                        };
                        prev = elements[i];
                    }
                    elements[elements.Count - 1].navigation = new Navigation
                    {
                        mode = Navigation.Mode.Explicit,
                        selectOnLeft = prev,
                    };
                }
            }
        }
        public static void LinkElementsVertical(IReadOnlyList<Selectable> elements)
        {
            if (elements.Count > 0)
            {
                if (elements.Count == 1)
                {
                    elements[0].navigation = new Navigation
                    {
                        mode = Navigation.Mode.Explicit,
                    };
                }
                else
                {
                    Selectable prev = null;
                    for (int i = 0; i < elements.Count - 1; i++)
                    {
                        elements[i].navigation = new Navigation
                        {
                            mode = Navigation.Mode.Explicit,
                            selectOnUp = prev,
                            selectOnDown = elements[i + 1],
                        };
                        prev = elements[i];
                    }
                    elements[elements.Count - 1].navigation = new Navigation
                    {
                        mode = Navigation.Mode.Explicit,
                        selectOnUp = prev,
                    };
                }
            }
        }

        private void OnInputModeChanged(InputMode inputMode)
        {
            if (inputMode == InputMode.Gamepad)
            {
                FocusStackTop();
            }
            else
            {
                SetSelection(null);
            }
        }
        private void FocusStackTop()
        {
            if (stack.Count > 0 && stack[^1].IsOpen)
            {
                stack[^1].FocusSelectable();
            }
        }

        public virtual void UpdateUserInterface()
        {
            if (stack.Count > 0)
            {
                int last = stack.Count - 1;
                for (int i = 0; i < stack.Count; i++)
                {
                    Menu menu = stack[i];
                    if (menu.IsOpen)
                    {
                        menu.UpdateWidget(i == last);
                    }
                }
            }

            UpdateFlags();
        }
        public virtual void UpdateInput(Controller controller)
        {
            if (stack.Count > 0)
            {
                Menu top = stack[^1];
                if (top.IsOpen)
                {
                    if (controller.Confirm.GetValue() && top.OnConfirm())
                        controller.Confirm.Use();
                    if (controller.Cancel.GetValue() && top.OnCancel())
                        controller.Cancel.Use();
                    if (controller.DirectionInput.GetValue() != Direction.None && top.OnDirection(controller.DirectionInput.GetValue()))
                        controller.DirectionInput.Use();
                }
            }
        }
        public virtual void UpdateLayout()
        {
            EventSystem eventSystem = EventSystem;
            if (eventSystem)
            {
                if (delayScrollLayout)
                {
                    delayScrollLayout = false;
                    currentSelection = null;
                    return;
                }

                // Check if current selection is visible in scroll rect
                GameObject selection = eventSystem.currentSelectedGameObject;
                if (selection != currentSelection)
                {
                    if (currentSelection && currentSelection.TryGetComponent(out RectTransform element))
                    {
                        ScrollRect scrollRect = currentSelection.GetComponentInParent<ScrollRect>();
                        if (scrollRect && scrollRect.vertical)
                        {
                            RectTransform viewport = scrollRect.viewport;
                            RectTransform content = scrollRect.content;

                            Rect viewportRect = GetRect(viewport);
                            Rect contentRect = GetRect(content);
                            if (contentRect.height > 0)
                            {
                                currentSelection = selection;

                                Rect elementRect = GetRect(element);

                                float scrollHeight = contentRect.height - viewportRect.height;
                                float contentTop = viewportRect.y - contentRect.y;
                                float elementTop = elementRect.y - contentRect.y;
                                float elementBottom = elementTop + elementRect.height;
                                if (elementTop < contentTop)
                                {
                                    ScrollTo(scrollRect, Mathf.Clamp01(elementTop / scrollHeight));
                                }
                                else if (elementBottom > contentTop + viewportRect.height)
                                {
                                    ScrollTo(scrollRect, Mathf.Clamp01((elementBottom - viewportRect.height) / scrollHeight));
                                }
                            }
                        }
                    }
                }
            }

            // Scroll to selection
            if (scrollingScrollRect)
            {
                scrollRectProgress += Time.deltaTime * 10;

                if (scrollRectProgress >= 1)
                {
                    scrollingScrollRect.verticalNormalizedPosition = scrollRectTargetValue;
                    scrollingScrollRect = null;
                }
                else
                {
                    float delta = 1 - Mathf.Pow(1 - scrollRectProgress, 2);
                    scrollingScrollRect.verticalNormalizedPosition = Mathf.Lerp(scrollRectOriginValue, scrollRectTargetValue, delta);
                }
            }
        }



        private T PushMenu<T>(T menu, bool instantiate) where T : Menu
        {
            if (instantiate)
            {
                menu = Instantiate(menu, RootTransform);
            }
            else
            {
                menu.transform.SetParent(RootTransform);
                if (menu.transform is RectTransform rectTransform)
                {
                    rectTransform.offsetMin = Vector2.zero;
                    rectTransform.offsetMax = Vector2.zero;
                }
                menu.transform.localScale = Vector3.one;
            }
            menu.Parent = this;
            stack.Add(menu);

            AllowInput |= menu.AllowInput;
            PauseGame |= menu.PauseGame;

            return menu;
        }

        public async UniTask<T> PushAwaitable<T>(T menu, bool instantiate = true) where T : Menu
        {
            menu = PushMenu(menu, instantiate);
            await menu.OpenAwaitable().Watch(this);
            return menu;
        }
        public T Push<T>(T menu, bool instantiate = true) where T : Menu
        {
            menu = PushMenu(menu, instantiate);
            menu.Open();
            return menu;
        }

        public async UniTask PopAwaitable()
        {
            if (stack.Count > 0)
            {
                Menu menu = stack[stack.Count - 1];
                menu.Parent = null;
                stack.RemoveAt(stack.Count - 1);
                UpdateFlags();
                // Restore focus
                FocusStackTop();
                await menu.CloseAwaitable().Watch(this);
            }
        }
        public void Pop()
        {
            PopAwaitable().Forget();
        }


        internal void RemoveFromStack(Menu menu)
        {
            for (int i = 0; i < stack.Count; i++)
            {
                if (ReferenceEquals(stack[i], menu))
                {
                    bool isTop = i == stack.Count - 1;
                    stack.RemoveAt(i);

                    UpdateFlags();
                    if (isTop)
                    {
                        // Restore focus
                        FocusStackTop();
                    }

                    return;
                }
            }
        }

        private void UpdateFlags()
        {
            AllowInput = false;
            PauseGame = false;

            foreach (var menu in stack)
            {
                AllowInput |= menu.AllowInput;
                PauseGame |= menu.PauseGame;
            }
        }


        public void HandleConfirm()
        {
            if (AppInstance.Instance.PlatformProfile.ActiveInputMode == InputMode.Gamepad && EventSystem)
            {
                GameObject selectedGameobject = EventSystem.currentSelectedGameObject;
                if (selectedGameobject)
                {
                    PointerEventData data = new(EventSystem.current);
                    ExecuteEvents.Execute(selectedGameobject, data, ExecuteEvents.pointerClickHandler);
                }
            }
        }
        public void HandleDirectionInput(Direction direction)
        {
            if (EventSystem)
            {
                // Send move event
                GameObject selectedGameobject = EventSystem.currentSelectedGameObject;
                if (selectedGameobject)
                {
                    AxisEventData data = new(EventSystem)
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
    }
}
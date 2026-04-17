using AggroBird.UnityExtend;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using static UnityEngine.EventSystems.ExecuteEvents;

namespace AggroBird.GameFramework
{
    public interface IMenu
    {
        public UserInterface Parent { get; }
    }

    public static class UserInterfaceUtility
    {
        public static Selectable[] GetSelectablesInParent(RectTransform parent)
        {
            List<Selectable> elements = new();
            int childCount = parent.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.gameObject.activeInHierarchy && child.TryGetComponent(out Selectable selectable))
                {
                    if (selectable.interactable && selectable.enabled)
                    {
                        elements.Add(selectable);
                    }
                }
            }
            return elements.ToArray();
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
        public static void LinkElementsHorizontal(RectTransform parent)
        {
            LinkElementsHorizontal(GetSelectablesInParent(parent));
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
                    for (int i = 0; i < elements.Count; i++)
                    {
                        elements[i].navigation = new Navigation
                        {
                            mode = Navigation.Mode.Explicit,
                            selectOnUp = elements[Mathfx.ModAbs(i - 1, elements.Count)],
                            selectOnDown = elements[Mathfx.ModAbs(i + 1, elements.Count)],
                        };
                    }
                }
            }
        }
        public static void LinkElementsVertical(RectTransform parent)
        {
            LinkElementsVertical(GetSelectablesInParent(parent));
        }
        public static void ReconnectSelectableNavigation(Selectable selectable)
        {
            if (selectable)
            {
                static Selectable Get(Selectable selectable, Direction direction)
                {
                    var nav = selectable.navigation;
                    return direction switch
                    {
                        Direction.Up => nav.selectOnUp,
                        Direction.Right => nav.selectOnRight,
                        Direction.Down => nav.selectOnDown,
                        Direction.Left => nav.selectOnLeft,
                        _ => throw new ArgumentException(),
                    };
                }
                static void Set(Selectable selectable, Direction direction, Selectable select)
                {
                    var nav = selectable.navigation;
                    switch (direction)
                    {
                        case Direction.Up:
                            nav.selectOnUp = select;
                            break;
                        case Direction.Right:
                            nav.selectOnRight = select;
                            break;
                        case Direction.Down:
                            nav.selectOnDown = select;
                            break;
                        case Direction.Left:
                            nav.selectOnLeft = select;
                            break;
                        default:
                            throw new ArgumentException();
                    }
                    selectable.navigation = nav;
                }
                var n0 = Get(selectable, Direction.Up);
                var n1 = Get(selectable, Direction.Right);
                var n2 = Get(selectable, Direction.Down);
                var n3 = Get(selectable, Direction.Left);
                bool b0 = n0;
                bool b1 = n1;
                bool b2 = n2;
                bool b3 = n3;
                if (b0 && b2) Set(n0, Direction.Down, n2);
                if (b1 && b3) Set(n1, Direction.Left, n3);
                if (b2 && b0) Set(n2, Direction.Up, n0);
                if (b3 && b1) Set(n3, Direction.Right, n1);
            }
        }
    }

    public abstract class UserInterface : MonoBehaviour
    {
        public Player Owner { get; private set; }

        public abstract RectTransform StackRoot { get; }
        public abstract EventSystem EventSystem { get; }

        private readonly List<Menu> stack = new();
        public int StackCount => stack.Count;
        public Menu Top => stack.Count > 0 ? stack[^1] : null;
        public Menu GetStackElement(int index) => stack[index];

        public bool AllowInput { get; private set; }
        public bool PauseGame { get; private set; }

        private GameObject currentSelectedScrollItem;
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

        private GameObject cursorLastRaycastedGameObject;
        public Selectable CurrentSelection { get; private set; }
        public FocusableSelectable CurrentFocusedSelectable { get; private set; }

        public event Action<Selectable> OnSelectionChangedEvent;
        public event Action<FocusableSelectable> OnFocusedSelectableChanged;

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

        private readonly List<RaycastResult> raycastResults = new();

        private static readonly FieldInfo pointerClickHandlerFieldInfo = typeof(ExecuteEvents).GetField("s_PointerClickHandler", BindingFlags.NonPublic | BindingFlags.Static);
        private EventFunction<IPointerClickHandler> defaultHandler;


        protected virtual void Awake()
        {
            if (pointerClickHandlerFieldInfo != null)
            {
                defaultHandler = (EventFunction<IPointerClickHandler>)pointerClickHandlerFieldInfo.GetValue(null);
                pointerClickHandlerFieldInfo?.SetValue(null, (EventFunction<IPointerClickHandler>)PointerClickHandler);
            }
        }
        protected virtual void OnDestroy()
        {
            pointerClickHandlerFieldInfo?.SetValue(null, defaultHandler);

            if (AppInstance.IsInitialized)
            {
                AppInstance.Instance.PlatformProfile.OnInputModeChanged -= OnInputModeChanged;
            }
        }

        public virtual void Initialize(Player owner)
        {
            if (AppInstance.IsInitialized)
            {
                AppInstance.Instance.PlatformProfile.OnInputModeChanged += OnInputModeChanged;
            }

            Owner = owner;
        }


        public void SetSelection(Selectable selectable)
        {
            EventSystem.SetSelectedGameObject(selectable ? selectable.gameObject : null);
        }
        public GameObject SelectedGameObject
        {
            get
            {
                EventSystem eventSystem = EventSystem;
                if (eventSystem)
                {
                    return eventSystem.currentSelectedGameObject;
                }
                return default;
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
                ClearFocusedSelectable();
                SetSelection(null);
            }
        }
        private bool FocusStackTop()
        {
            if (stack.Count > 0 && stack[^1].IsOpen)
            {
                return stack[^1].FocusSelectable();
            }
            return false;
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
        public virtual void UpdateInput(Controller controller, bool inputEnabled)
        {
            if (stack.Count > 0)
            {
                if (!SelectedGameObject)
                {
                    if (controller.Default.DirectionInput.GetValue() != Direction.None)
                    {
                        if (FocusStackTop())
                        {
                            controller.Default.DirectionInput.Use();
                        }
                    }
                }

                Menu top = stack[^1];
                if (top.IsOpen && top.ConsumeEvents)
                {
                    if (controller.Default.Confirm.GetValue() && top.OnConfirm())
                        controller.Default.Confirm.Use();
                    if (controller.Default.Cancel.GetValue() && top.OnCancel())
                        controller.Default.Cancel.Use();
                    var directionInput = controller.Default.DirectionInput.GetValue();
                    if (directionInput != Direction.None && top.OnDirection(directionInput))
                        controller.Default.DirectionInput.Use();
                }
            }

            if (CurrentFocusedSelectable && EventSystem.currentSelectedGameObject != CurrentFocusedSelectable.gameObject)
            {
                if (!EventSystem.currentSelectedGameObject)
                {
                    ClearFocusedSelectable();
                }
                else if (EventSystem.currentSelectedGameObject.GetComponentInParent<FocusableSelectable>() != CurrentFocusedSelectable)
                {
                    ClearFocusedSelectable();
                }
                else
                {
                    SetSelection(CurrentFocusedSelectable);
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
                    currentSelectedScrollItem = null;
                    return;
                }

                // Update selection
                GameObject selection = eventSystem.currentSelectedGameObject;
                if (AppInstance.Instance.PlatformProfile.ActiveInputMode == InputMode.Gamepad)
                {
                    bool TryGetSelectable(out Selectable selectable)
                    {
                        if (!selection)
                        {
                            selectable = null;
                            return false;
                        }
                        return selection.TryGetComponent(out selectable);
                    }

                    if (TryGetSelectable(out Selectable selectable))
                    {
                        if (!ReferenceEquals(CurrentSelection, selectable))
                        {
                            CurrentSelection = selectable;
                            OnSelectionChangedEvent?.Invoke(CurrentSelection);
                            OnSelectionChanged(selectable);
                        }
                    }
                    else
                    {
                        if (!ReferenceEquals(CurrentSelection, null))
                        {
                            CurrentSelection = null;
                            OnSelectionChangedEvent?.Invoke(null);
                            OnSelectionChanged(null);
                        }
                    }

                    cursorLastRaycastedGameObject = null;
                }
                else if (eventSystem.currentInputModule is InputSystemUIInputModule inputModule)
                {
                    var last = inputModule.GetLastRaycastResult(0);

                    if (last.gameObject != cursorLastRaycastedGameObject)
                    {
                        cursorLastRaycastedGameObject = last.gameObject;

                        bool TryGetRootMostParentSelectable(out Selectable selectable)
                        {
                            if (cursorLastRaycastedGameObject)
                            {
                                selectable = cursorLastRaycastedGameObject.GetComponentInParent<Selectable>();
                                if (selectable)
                                {
                                    while (true)
                                    {
                                        var parent = selectable.transform.parent;
                                        if (!parent)
                                        {
                                            break;
                                        }
                                        var next = parent.GetComponentInParent<Selectable>();
                                        if (!next)
                                        {
                                            break;
                                        }
                                        selectable = next;
                                    }
                                }
                                return selectable;
                            }

                            selectable = null;
                            return false;
                        }

                        if (TryGetRootMostParentSelectable(out Selectable selectable))
                        {
                            if (!ReferenceEquals(CurrentSelection, selectable))
                            {
                                CurrentSelection = selectable;
                                OnSelectionChangedEvent?.Invoke(CurrentSelection);
                                OnSelectionChanged(selectable);
                            }
                        }
                        else
                        {
                            if (!ReferenceEquals(CurrentSelection, null))
                            {
                                CurrentSelection = null;
                                OnSelectionChangedEvent?.Invoke(null);
                                OnSelectionChanged(null);
                            }
                        }
                    }
                }

                // Check if current selection is visible in scroll rect
                if (selection != currentSelectedScrollItem)
                {
                    if (selection && selection.TryGetComponent(out RectTransform element))
                    {
                        ScrollRect scrollRect = selection.GetComponentInParent<ScrollRect>();
                        if (scrollRect && scrollRect.vertical)
                        {
                            RectTransform viewport = scrollRect.viewport;
                            RectTransform content = scrollRect.content;

                            Rect viewportRect = GetRect(viewport);
                            Rect contentRect = GetRect(content);
                            if (contentRect.height > 0)
                            {
                                currentSelectedScrollItem = selection;

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
                scrollRectProgress += Time.unscaledDeltaTime * 10;

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

        protected virtual void OnSelectionChanged(Selectable selectable)
        {

        }
        protected virtual void OnSelect(Selectable selectable)
        {

        }

        private void PointerClickHandler(IPointerClickHandler handler, BaseEventData eventData)
        {
            if (eventData is PointerEventData pointerEvent)
            {
                var pointerPress = pointerEvent.pointerPress;
                if (pointerPress)
                {
                    var selectable = pointerPress.GetComponentInParent<Selectable>();
                    if (selectable)
                    {
                        var userInterface = pointerPress.GetComponentInParent<IMenu>();
                        if (userInterface != null && userInterface.Parent)
                        {
                            userInterface.Parent.OnSelect(selectable);
                        }
                    }
                }
            }

            defaultHandler?.Invoke(handler, eventData);
        }

        private T PushMenu<T>(T menu, bool instantiate) where T : Menu
        {
            if (instantiate)
            {
                menu = Instantiate(menu, StackRoot);
            }
            else
            {
                menu.transform.SetParent(StackRoot);
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


        public bool HandleConfirm()
        {
            if (CurrentFocusedSelectable)
            {
                if (AppInstance.Instance.PlatformProfile.ActiveInputMode == InputMode.Gamepad)
                {
                    ClearFocusedSelectable();
                    return true;
                }
            }

            if (EventSystem)
            {
                GameObject selectedGameobject = EventSystem.currentSelectedGameObject;
                if (selectedGameobject)
                {
                    PointerEventData data = new(EventSystem.current);
                    Execute(selectedGameobject, data, pointerClickHandler);

                    if (selectedGameobject.TryGetComponent(out FocusableSelectable gamepadSelectable))
                    {
                        FocusSelectable(gamepadSelectable);
                        OnSelect(gamepadSelectable);
                    }
                    else if (selectedGameobject.TryGetComponent<Selectable>(out var selectable))
                    {
                        OnSelect(selectable);
                    }

                    return true;
                }
            }

            return false;
        }
        public bool HandleCancel()
        {
            if (CurrentFocusedSelectable)
            {
                ClearFocusedSelectable();
                return true;
            }
            return false;
        }
        public bool HandleDirectionInput(Direction direction)
        {
            if (EventSystem)
            {
                if (CurrentFocusedSelectable)
                {
                    // Send direction event
                    CurrentFocusedSelectable.OnDirectionInput(direction);

                    return true;
                }
                else if (EventSystem.currentSelectedGameObject is GameObject selectedGameobject)
                {
                    // Send move event
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

                    return true;
                }
            }

            return false;
        }

        internal void FocusSelectable(FocusableSelectable gamepadSelectable)
        {
            if (CurrentFocusedSelectable != gamepadSelectable)
            {
                ClearFocusedSelectable();
                CurrentFocusedSelectable = gamepadSelectable;
                CurrentFocusedSelectable.OnGainFocus();
                OnFocusedSelectableChanged?.Invoke(CurrentFocusedSelectable);
            }
        }
        private void ClearFocusedSelectable()
        {
            if (CurrentFocusedSelectable)
            {
                CurrentFocusedSelectable.OnLoseFocus();
                CurrentFocusedSelectable = null;
                OnFocusedSelectableChanged?.Invoke(CurrentFocusedSelectable);
            }
        }
    }
}
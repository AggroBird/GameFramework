using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

namespace AggroBird.GameFramework
{
    public abstract class Widget : MonoBehaviour
    {
        public enum WidgetState
        {
            Closed = 0,
            Opening,
            Open,
            Closing,
        }

        public WidgetState State { get; private set; }
        public bool IsOpen => State == WidgetState.Open;


        private bool OpenWidget()
        {
            switch (State)
            {
                case WidgetState.Closed:
                    State = WidgetState.Opening;
                    OnOpen();
                    return true;
                case WidgetState.Closing:
                    State = WidgetState.Opening;
                    return false;
            }
            return false;
        }
        private bool CloseWidget()
        {
            switch (State)
            {
                case WidgetState.Open:
                    State = WidgetState.Closing;
                    OnClose();
                    return true;
                case WidgetState.Opening:
                    State = WidgetState.Closing;
                    return true;
            }
            return false;
        }

        public void Open()
        {
            OpenWidget();
        }
        public void Close()
        {
            CloseWidget();
        }

        public async UniTask OpenAwaitable()
        {
            if (OpenWidget())
            {
                await UniTask.WaitUntil(() => State != WidgetState.Opening).Watch(this);
                return;
            }
        }
        public async UniTask CloseAwaitable()
        {
            if (CloseWidget())
            {
                await UniTask.WaitUntil(() => State == WidgetState.Closed).Watch(this).SuppressCancellationThrow();
                return;
            }
        }

        private void OnOpenAnimationFinished()
        {
            switch (State)
            {
                case WidgetState.Opening:
                    State = WidgetState.Open;
                    OnOpened();
                    break;
                case WidgetState.Closing:
                    OnClose();
                    break;
            }
        }
        private void OnCloseAnimationFinished()
        {
            switch (State)
            {
                case WidgetState.Closing:
                    State = WidgetState.Closed;
                    OnClosed();
                    break;
                case WidgetState.Opening:
                    OnOpen();
                    break;
            }
        }

        public event Action onOpen;
        public event Action onOpened;
        public event Action onClose;
        public event Action onClosed;

        protected virtual void OnOpen()
        {
            onOpen?.Invoke();
        }
        protected virtual void OnOpened()
        {
            onOpened?.Invoke();
        }
        protected virtual void OnClose()
        {
            onClose?.Invoke();
        }
        protected virtual void OnClosed()
        {
            onClosed?.Invoke();
        }
    }
}
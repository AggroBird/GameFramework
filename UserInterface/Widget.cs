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

        public event Action onOpen;
        public event Action onOpened;
        public event Action onClose;
        public event Action onClosed;

        protected virtual void OnOpen()
        {
            State = WidgetState.Opening;
            onOpen?.Invoke();
        }
        protected virtual void OnOpened()
        {
            State = WidgetState.Open;
            onOpened?.Invoke();
        }
        protected virtual void OnClose()
        {
            State = WidgetState.Closing;
            onClose?.Invoke();
        }
        protected virtual void OnClosed()
        {
            State = WidgetState.Closed;
            onClosed?.Invoke();
        }
    }
}
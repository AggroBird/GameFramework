using System;
using UnityEngine;

namespace AggroBird.GameFramework
{
    [Serializable]
    public abstract class InteractTooltipContent
    {

    }

    public interface IInteractable
    {
        InteractTooltipContent TooltipContent { get; }
        Vector3 InteractPosition { get; }
        bool CanInteract(Interactor interactor);
        public int Priority { get; }

        void BeginInteract(Interactor interactor);
        void UpdateInteract(Interactor interactor);
        void EndInteract(Interactor interactor);
    }

    public abstract class Interactable : MonoBehaviour, IInteractable
    {
        public abstract InteractTooltipContent TooltipContent { get; }
        [SerializeField]
        protected Vector3 interactOffset = Vector3.zero;
        [SerializeField]
        protected bool scaleInteractOffset = true;
        public virtual Vector3 InteractPosition
        {
            get
            {
                if (scaleInteractOffset)
                {
                    return transform.TransformPoint(interactOffset);
                }
                else
                {
                    return transform.position + transform.rotation * interactOffset;
                }
            }
        }

        public virtual int Priority { get => 0; }

        public abstract bool CanInteract(Interactor interactor);

        public event Action<Interactor> OnBeginInteract;
        public event Action<Interactor> OnUpdateInteract;
        public event Action<Interactor> OnEndInteract;


        public virtual void BeginInteract(Interactor interactor)
        {
            OnBeginInteract?.Invoke(interactor);
        }
        public virtual void UpdateInteract(Interactor interactor)
        {
            OnUpdateInteract?.Invoke(interactor);
        }
        public virtual void EndInteract(Interactor interactor)
        {
            OnEndInteract?.Invoke(interactor);
        }


#if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(InteractPosition, 0.2f);
        }
#endif
    }
}

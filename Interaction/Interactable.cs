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

        void BeginInteract(Interactor interactor);
        void UpdateInteract(Interactor interactor);
        void EndInteract(Interactor interactor);
    }

    public abstract class Interactable : MonoBehaviour, IInteractable
    {
        public abstract InteractTooltipContent TooltipContent { get; }
        [SerializeField] protected Vector3 interactOffset = Vector3.zero;
        public virtual Vector3 InteractPosition { get => transform.TransformPoint(interactOffset); }

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

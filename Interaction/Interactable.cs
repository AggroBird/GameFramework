using System;
using UnityEngine;

namespace AggroBird.GameFramework
{
    [Serializable]
    public abstract class InteractTooltipContent
    {

    }

    public abstract class Interactable : MonoBehaviour
    {
        public abstract InteractTooltipContent TooltipContent { get; }
        [SerializeField] private Vector3 interactOffset = Vector3.zero;
        public virtual Vector3 InteractPosition { get => transform.TransformPoint(interactOffset); }

        public abstract bool CanInteract(Interactor interactor);

        public event Action<Interactor> OnBeginInteract;
        public event Action<Interactor> OnUpdateInteract;
        public event Action<Interactor> OnEndInteract;


        protected internal virtual void BeginInteract(Interactor interactor)
        {
            OnBeginInteract?.Invoke(interactor);
        }
        protected internal virtual void UpdateInteract(Interactor interactor)
        {
            OnUpdateInteract?.Invoke(interactor);
        }
        protected internal virtual void EndInteract(Interactor interactor)
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

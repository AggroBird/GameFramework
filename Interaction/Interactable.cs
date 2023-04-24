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


        protected internal virtual void BeginInteract(Interactor interactor)
        {

        }
        protected internal virtual void UpdateInteract(Interactor interactor)
        {

        }
        protected internal virtual void EndInteract(Interactor interactor)
        {

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

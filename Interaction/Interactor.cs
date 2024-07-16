using System.Collections.Generic;
using UnityEngine;

namespace AggroBird.GameFramework
{
    public class Interactor : MonoBehaviour
    {
        [field: SerializeField] public Entity Entity { get; private set; }

        private static bool IsValid(IInteractable iteractable)
        {
            if (iteractable is Object obj)
            {
                return obj;
            }
            return iteractable != null;
        }

        private readonly List<IInteractable> overlappingInteractables = new();
        private IInteractable nearestInteractable = null;
        private IInteractable currentInteractable = null;

        public IInteractable CurrentInteractable => IsValid(currentInteractable) ? currentInteractable : IsValid(nearestInteractable) ? nearestInteractable : null;

        private ButtonSwitch inputState;


        protected virtual void Start()
        {
            enabled = Entity;
        }

        protected virtual void OnDestroy()
        {
            EndInteract();
        }


        protected virtual void FixedUpdate()
        {
            UpdateInteractables();
        }

        public virtual void UpdateInput(bool interact)
        {
            // Update input
            inputState.Update(interact);
            switch (inputState.State)
            {
                case ButtonState.Pressed:
                    BeginInteract();
                    break;
                case ButtonState.Released:
                    EndInteract();
                    break;
            }

            if (IsValid(currentInteractable))
            {
                currentInteractable.UpdateInteract(this);
            }
        }

        private void UpdateInteractables()
        {
            // Update nearest
            nearestInteractable = null;
            if (overlappingInteractables.Count > 0)
            {
                float nearestDist = float.MaxValue;
                for (int i = 0; i < overlappingInteractables.Count; i++)
                {
                    var interactable = overlappingInteractables[i];
                    if ((interactable is not Behaviour component || (component && component.isActiveAndEnabled)) && interactable.CanInteract(this))
                    {
                        float dist = (interactable.InteractPosition - transform.position).sqrMagnitude;
                        if (dist < nearestDist)
                        {
                            nearestInteractable = interactable;
                            nearestDist = dist;
                        }
                    }
                    else if (ReferenceEquals(currentInteractable, interactable))
                    {
                        EndInteract();
                    }
                }
                overlappingInteractables.Clear();
            }
        }

        public virtual void BeginInteract()
        {
            if (!IsValid(currentInteractable))
            {
                if (IsValid(nearestInteractable))
                {
                    currentInteractable = nearestInteractable;
                    currentInteractable.BeginInteract(this);
                }
            }
        }
        public virtual void EndInteract()
        {
            IInteractable interactable = currentInteractable;
            if (IsValid(interactable))
            {
                currentInteractable = null;
                interactable.EndInteract(this);
            }
        }


        private readonly List<IInteractable> overlap = new List<IInteractable>();

        protected void OnTriggerEnter(Collider trigger)
        {
            trigger.GetComponentsInParent(false, overlap);
            if (overlap.Count > 0)
            {
                foreach (var interactable in overlap)
                {
                    if (!overlappingInteractables.Contains(interactable))
                    {
                        overlappingInteractables.Add(interactable);
                    }
                }
            }
        }
        protected void OnTriggerStay(Collider trigger)
        {
            trigger.GetComponentsInParent(false, overlap);
            if (overlap.Count > 0)
            {
                foreach (var interactable in overlap)
                {
                    if (!overlappingInteractables.Contains(interactable))
                    {
                        overlappingInteractables.Add(interactable);
                    }
                }
            }
        }

        protected virtual void OnDisable()
        {
            overlappingInteractables.Clear();
        }
    }
}

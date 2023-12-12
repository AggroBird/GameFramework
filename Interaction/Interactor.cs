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

        private readonly List<IInteractable> interactables = new();
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


        protected virtual void Update()
        {
            UpdateInteractables();
        }

        public virtual void UpdateInput(bool interact)
        {
            UpdateInteractables();

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
            // Filter invalids
            for (int i = 0; i < interactables.Count;)
            {
                if (!IsValid(interactables[i]))
                {
                    interactables.RemoveAt(i);
                    continue;
                }
                i++;
            }

            // Update nearest
            nearestInteractable = null;
            if (interactables.Count > 0)
            {
                float nearestDist = float.MaxValue;
                for (int i = 0; i < interactables.Count; i++)
                {
                    var interactable = interactables[i];
                    if ((interactable is not Behaviour component || component.isActiveAndEnabled) && interactable.CanInteract(this))
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

        protected virtual void OnTriggerEnter(Collider trigger)
        {
            trigger.GetComponentsInParent(false, overlap);
            if (overlap.Count > 0)
            {
                foreach (var interactable in overlap)
                {
                    if (!interactables.Contains(interactable))
                    {
                        interactables.Add(interactable);
                    }
                }
            }
        }
        protected virtual void OnTriggerExit(Collider trigger)
        {
            trigger.GetComponentsInParent(false, overlap);
            if (overlap.Count > 0)
            {
                foreach (var interactable in overlap)
                {
                    interactables.Remove(interactable);

                    if (ReferenceEquals(currentInteractable, interactable))
                    {
                        EndInteract();
                    }
                }
            }
        }

        protected virtual void OnDisable()
        {
            interactables.Clear();
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace AggroBird.GameFramework
{
    public class Interactor : MonoBehaviour
    {
        [field: SerializeField] public Entity Entity { get; private set; }

        private readonly List<Interactable> interactables = new();
        private Interactable nearestInteractable = null;
        private Interactable currentInteractable = null;

        public Interactable CurrentInteractable => currentInteractable ? currentInteractable : nearestInteractable;


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

        public virtual void UpdateInput(ButtonStateObject input)
        {
            UpdateInteractables();

            // Update input
            switch (input.state)
            {
                case ButtonState.Pressed:
                    BeginInteract();
                    break;
                case ButtonState.Released:
                    EndInteract();
                    break;
            }

            if (currentInteractable)
            {
                currentInteractable.UpdateInteract(this);
            }
        }

        private void UpdateInteractables()
        {
            // Filter invalids
            for (int i = 0; i < interactables.Count;)
            {
                if (!interactables[i])
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
                    if (interactables[i].isActiveAndEnabled && interactables[i].CanInteract(this))
                    {
                        float dist = (interactables[i].InteractPosition - transform.position).sqrMagnitude;
                        if (dist < nearestDist)
                        {
                            nearestInteractable = interactables[i];
                            nearestDist = dist;
                        }
                    }
                }
            }
        }

        public virtual void BeginInteract()
        {
            if (!currentInteractable)
            {
                if (nearestInteractable)
                {
                    currentInteractable = nearestInteractable;
                    currentInteractable.BeginInteract(this);
                }
            }
        }
        public virtual void EndInteract()
        {
            Interactable interactable = currentInteractable;
            if (interactable)
            {
                currentInteractable = null;
                interactable.EndInteract(this);
            }
        }


        private readonly List<Interactable> overlap = new List<Interactable>();

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
                }
            }
        }

        protected virtual void OnDisable()
        {
            interactables.Clear();
        }
    }
}

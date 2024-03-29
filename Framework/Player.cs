using System.Collections.Generic;
using UnityEngine;

namespace AggroBird.GameFramework
{
    public abstract class Player : ScriptableObject
    {
        public AppInstance App { get; private set; }

        // Pawn that this player is controlling
        internal Pawn pawn;
        public Pawn Pawn => pawn;
        public void SetPawn(Pawn newPawn)
        {
            if (newPawn != pawn)
            {
                Pawn prevPawn = pawn;
                if (!newPawn)
                {
                    pawn = null;
                    if (prevPawn)
                    {
                        prevPawn.Owner = null;
                        prevPawn.CallOnOwnerChanged(this, null);
                    }
                    OnPawnChanged(prevPawn, null);
                }
                else
                {
                    pawn = newPawn;
                    Player prevOwner = newPawn.Owner;
                    newPawn.Owner = this;
                    if (prevOwner)
                    {
                        prevOwner.pawn = null;
                        prevOwner.OnPawnChanged(newPawn, null);
                    }
                    if (prevPawn)
                    {
                        prevPawn.Owner = null;
                        prevPawn.CallOnOwnerChanged(this, null);
                    }
                    OnPawnChanged(prevPawn, newPawn);
                    newPawn.CallOnOwnerChanged(prevOwner, this);
                }
            }
        }
        public bool TryGetPawn<T>(out T result) where T : Pawn => result = pawn as T;

        // Player controller object
        public Controller Controller { get; protected set; }
        public bool TryGetController<T>(out T result) where T : Controller => result = Controller as T;

        // User interface (optional)
        public UserInterface UI { get; protected set; }
        public bool TryGetUserInterface<T>(out T result) where T : UserInterface => result = UI as T;

        private readonly List<PlayerCamera> cameraStack = new();
        public PlayerCamera CurrentCamera => cameraStack.Count > 0 ? cameraStack[^1] : null;


        public bool GamePaused { get; private set; }

        private bool inputEnabled = true;
        public virtual bool InputEnabled
        {
            get => inputEnabled && (!AppInstance.TryGetInstance(out AppInstance instance) || instance.InputEnabled);
        }
        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
        }


        public virtual void Initialize(AppInstance app)
        {
            App = app;
        }

        public virtual void Shutdown()
        {

        }

        public virtual void UpdateInput(int index)
        {
            if (Controller)
            {
                Controller.UpdateInput(this, index, InputEnabled);

                if (pawn)
                {
                    pawn.UpdateInput(Controller);
                }

                if (UI)
                {
                    // Update widgets
                    UI.UpdateUserInterface();
                    // Update input (after widgets have consumed input events)
                    UI.UpdateInput(Controller);
                }
            }
        }
        public virtual void UpdateUserInterface()
        {
            if (UI)
            {
                // Update layout
                UI.UpdateLayout();
            }
        }

        protected virtual void OnPawnChanged(Pawn previousPawn, Pawn newPawn)
        {

        }


        public void RegisterCamera(PlayerCamera camera)
        {
            if (!cameraStack.Contains(camera))
            {
                cameraStack.Add(camera);
            }
        }
        public void UnregisterCamera(PlayerCamera camera)
        {
            cameraStack.Remove(camera);
        }
    }
}
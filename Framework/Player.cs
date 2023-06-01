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
        [SerializeField] private Controller controllerPrefab = default;
        public Controller Controller { get; private set; }
        public bool TryGetController<T>(out T result) where T : Controller => result = Controller as T;

        // User interface (optional)
        public UserInterface UI { get; protected set; }
        public bool TryGetUserInterface<T>(out T result) where T : UserInterface => result = UI as T;

        private readonly List<IPlayerCamera> cameraStack = new List<IPlayerCamera>();
        public Camera CurrentCamera => cameraStack.Count > 0 ? cameraStack[^1].Camera : null;


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


        public virtual void Initialize(AppInstance app, bool createController = true)
        {
            App = app;

            if (createController && controllerPrefab)
            {
                Controller = Instantiate(controllerPrefab);
            }
        }

        public virtual void Shutdown()
        {
            if (Controller) Destroy(Controller);
            if (UI) Destroy(UI.gameObject);
        }

        public virtual void UpdateInput()
        {
            if (Controller)
            {
                Controller.UpdateInput(this, InputEnabled);

                if (pawn)
                {
                    pawn.UpdateInput(Controller);
                }
            }
        }
        public virtual void UpdateUserInterface()
        {
            if (UI)
            {
                // Update widgets
                UI.UpdateUserInterface();
                // Update input (after widgets have consumed input events)
                if (Controller)
                {
                    UI.UpdateInput(Controller);
                }
                // Update layout
                UI.UpdateLayout();
            }
        }

        protected virtual void OnPawnChanged(Pawn previousPawn, Pawn newPawn)
        {

        }


        public void RegisterCamera(IPlayerCamera camera)
        {
            if (!cameraStack.Contains(camera))
            {
                cameraStack.Add(camera);
            }
        }
        public void UnregisterCamera(IPlayerCamera camera)
        {
            cameraStack.Remove(camera);
        }
    }
}
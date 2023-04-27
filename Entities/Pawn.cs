using UnityEngine;

namespace AggroBird.GameFramework
{
    [DisallowMultipleComponent]
    public class Pawn : Entity
    {
        [SerializeField] private Dummy dummy;
        public Dummy Dummy => dummy;
        public bool TryGetDummy(out Dummy dummy)
        {
            dummy = this.dummy;
            return dummy;
        }

        public bool rotateCamera = false;

        public Player Owner { get; internal set; }
        public bool TryGetOwner<T>(out T result) where T : Player => result = Owner as T;


        public virtual Interactor Interactor { get; }


        public virtual void UpdateInput(Controller controller)
        {

        }

        protected virtual void OnOwnerChanged(Player previousOwner, Player newOwner)
        {

        }
        internal void CallOnOwnerChanged(Player previousOwner, Player newOwner)
        {
            OnOwnerChanged(previousOwner, newOwner);
        }

        public virtual void Teleport(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
        }
    }
}

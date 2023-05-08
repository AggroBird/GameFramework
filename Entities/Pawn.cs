using UnityEngine;

namespace AggroBird.GameFramework
{
    [DisallowMultipleComponent]
    public class Pawn : Entity
    {
        [SerializeField] private Dummy dummy;

        private Dummy FetchDummy()
        {
            if (dummy)
            {
                return dummy;
            }

            int childCount = transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                if (transform.TryGetComponent(out dummy))
                {
                    return dummy;
                }
            }

            return null;
        }
        public Dummy Dummy => FetchDummy();
        public bool TryGetDummy<T>(out Dummy dummy) where T : Dummy
        {
            dummy = FetchDummy() as T;
            return dummy;
        }

        public bool rotateCamera = false;

        public Player Owner { get; internal set; }
        public bool TryGetOwner<T>(out T result) where T : Player => result = Owner as T;

        public virtual Vector3 Center => transform.position + Vector3.up;


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

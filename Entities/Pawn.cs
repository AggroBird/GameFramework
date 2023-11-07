using AggroBird.UnityExtend;
using System;
using UnityEngine;

namespace AggroBird.GameFramework
{
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
                if (transform.GetChild(i).TryGetComponent(out dummy))
                {
                    return dummy;
                }
            }

            return null;
        }
        public Dummy Dummy
        {
            get => FetchDummy();
            set => dummy = value;
        }
        public bool TryGetDummy<T>(out T dummy) where T : Dummy
        {
            dummy = FetchDummy() as T;
            return dummy;
        }

        public AutoFollowRotationMode cameraAutoFollowRotationMode = AutoFollowRotationMode.None;
        [NonSerialized] public Rotator2 cameraAutoFollowSpeedMultiplier = new(1, 1);
        [NonSerialized] public bool cameraAutoFollowAllowReverse = false;

        public Player Owner { get; internal set; }
        public bool TryGetOwner<T>(out T result) where T : Player => result = Owner as T;

        public virtual Vector3 Center => transform.position + Vector3.up;

        public event Action<Pawn> OnDestroyEvent;


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

        protected virtual void OnDestroy()
        {
            OnDestroyEvent?.Invoke(this);
        }
    }
}

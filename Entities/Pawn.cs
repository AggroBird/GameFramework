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

        [Header("Camera Settings")]
        public AutoFollowRotationMode cameraAutoFollowRotationMode = AutoFollowRotationMode.None;
        public bool allowCameraRotation = true;
        [Space]
        [Clamped(min: 0)] public float linearFollowSpeed = 10;
        public Rotator2 angularFollowSpeed = new(5, 5);
        [Space]
        [Clamped(0, 90)]
        public float pitch = 10;
        public Vector3 originOffset = new(0, 2, 0);
        public Vector3 followOffset = new(0, 3, -5);
        [Space]
        public FloatRange pitchRange = new(-30, 60);


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

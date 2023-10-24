using AggroBird.UnityExtend;
using UnityEngine;

namespace AggroBird.GameFramework
{
    public abstract class PlayerCamera : MonoBehaviour
    {
        public enum UpdateMode
        {
            Update,
            LateUpdate,
            FixedUpdate,
        }

        public abstract Camera Camera { get; }

        public virtual Vector3 Position => transform.position;
        public virtual Quaternion Rotation => transform.rotation;
        public virtual float FieldOfView
        {
            get => Camera.fieldOfView;
            set => Camera.fieldOfView = value;
        }

        public UpdateMode updateInputMode = UpdateMode.LateUpdate;
        public UpdateMode updateTransformMode = UpdateMode.LateUpdate;
        [Space]
        [Clamped(min: 0)] public int playerIndex = 0;

        public Player Owner { get; private set; }


        protected virtual void Update()
        {
            if (updateInputMode == UpdateMode.Update)
            {
                UpdateInput();
            }
            if (updateTransformMode == UpdateMode.Update)
            {
                UpdateTransform();
            }
        }
        protected virtual void LateUpdate()
        {
            if (updateInputMode == UpdateMode.LateUpdate)
            {
                UpdateInput();
            }
            if (updateTransformMode == UpdateMode.LateUpdate)
            {
                UpdateTransform();
            }
        }
        protected virtual void FixedUpdate()
        {
            if (updateInputMode == UpdateMode.FixedUpdate)
            {
                UpdateInput();
            }
            if (updateTransformMode == UpdateMode.FixedUpdate)
            {
                UpdateTransform();
            }
        }

        protected virtual void UpdateInput()
        {
            if (AppInstance.TryGetInstance(out AppInstance instance) && instance.TryGetPlayer(playerIndex, out Player player))
            {
                if (player != Owner)
                {
                    if (Owner)
                    {
                        Owner.UnregisterCamera(this);
                    }

                    player.RegisterCamera(this);
                    Owner = player;
                }
            }
            else if (Owner)
            {
                Owner.UnregisterCamera(this);
                Owner = null;
            }
        }
        protected virtual void UpdateTransform()
        {

        }


        protected virtual void OnEnable()
        {

        }
        protected virtual void OnDisable()
        {
            if (Owner)
            {
                Owner.UnregisterCamera(this);
                Owner = null;
            }
        }
    }
}

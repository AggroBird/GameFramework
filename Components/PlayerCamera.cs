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

        public UpdateMode updateMode = UpdateMode.LateUpdate;


        protected virtual void Update()
        {
            if (updateMode == UpdateMode.Update)
            {
                UpdateInput();

                UpdateTransform();
            }
        }

        protected virtual void LateUpdate()
        {
            // Also perform input for fixed update
            if (updateMode == UpdateMode.LateUpdate || updateMode == UpdateMode.FixedUpdate)
            {
                UpdateInput();
            }

            if (updateMode == UpdateMode.LateUpdate)
            {
                UpdateTransform();
            }
        }

        protected virtual void FixedUpdate()
        {
            if (updateMode == UpdateMode.FixedUpdate)
            {
                UpdateTransform();
            }
        }

        protected virtual void UpdateInput()
        {

        }
        protected virtual void UpdateTransform()
        {

        }
    }
}

using UnityEngine;

namespace AggroBird.GameFramework
{
    public abstract class PlayerCamera : MonoBehaviour
    {
        public abstract Camera Camera { get; }

        public virtual Vector3 Position => transform.position;
        public virtual Quaternion Rotation => transform.rotation;
        public virtual float FieldOfView
        {
            get => Camera.fieldOfView;
            set => Camera.fieldOfView = value;
        }
    }
}

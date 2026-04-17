using UnityEngine;

namespace AggroBird.GameFramework
{
    public abstract class Movement : MonoBehaviour
    {
        public virtual void Teleport(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
        }
        public virtual void Teleport(Vector3 position)
        {
            Teleport(position, transform.rotation);
        }
    }
}
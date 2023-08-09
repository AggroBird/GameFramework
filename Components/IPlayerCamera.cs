using UnityEngine;

namespace AggroBird.GameFramework
{
    public interface IPlayerCamera
    {
        public Camera Camera { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public float FieldOfView { get; }
    }
}
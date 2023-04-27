using AggroBird.UnityEngineExtend;
using System.Collections.Generic;
using UnityEngine;

namespace AggroBird.GameFramework
{
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public class VehicleMovement : Movement
    {
        [SerializeField, HideInInspector]
        [UnityEngine.Serialization.FormerlySerializedAs("rigidbodyComponent")]
        protected new Rigidbody rigidbody = default;
        public Rigidbody Rigidbody => rigidbody;
        [SerializeField, HideInInspector]
        [UnityEngine.Serialization.FormerlySerializedAs("colliderComponent")]
        protected new CapsuleCollider collider = default;
        public Collider Collider => collider;

        [Header("Vehicle Settings")]
        [SerializeField, Clamped(min: 0.1f)] protected float collisionRadius = 0.5f;
        [SerializeField, Clamped(min: 0.2f)] protected float collisionHeight = 2;
        [Space]
        [SerializeField, Min(0)] protected float defaultSpeed = 4;
        [SerializeField, Min(0)] protected float maxSpeed = 10;
        [SerializeField, Min(0)] protected float forwardAcceleration = 5;
        [Space]
        [SerializeField, Min(0)] protected float reverseSpeed = 2;
        [SerializeField, Min(0)] protected float reverseAcceleration = 1;
        [Space]
        [SerializeField, Min(0)] protected float steerAngle = 70;
        [Space]
        [SerializeField, Min(0)] protected float tyreFriction = 5;
        [SerializeField, Clamped(min: 0, max: 90)] protected float maxGroundAngle = 50;
        [Space]
        [SerializeField] protected AnimationCurve rolloutCurve;

        [System.Serializable]
        protected struct Seat
        {
            [SerializeField] private Transform transform;
            public Transform Transform => transform;

            [System.NonSerialized] public CharacterMovement passenger;
        }
        [Space]
        [SerializeField] protected Seat[] seats = null;

        public int SeatCount => seats.Length;
        public CharacterMovement GetPassenger(int index)
        {
            if ((uint)index >= (uint)seats.Length)
            {
                return null;
            }
            return seats[index].passenger;
        }

        private float GetRollout(float velz) => maxSpeed > 0 ? rolloutCurve.Evaluate(Mathf.Abs(velz) / maxSpeed) : 0;

        private PhysicMaterial physicMaterial = default;


        private Vector3 localVelocity = Vector3.zero;
        private float steerValue = 0;

        private List<Vector3> contactNormals = new List<Vector3>();
        private Vector3 groundNormal = Vector3.up;
        private bool isGrounded = false;

        public Vector3 Velocity => rigidbody.velocity;
        public Vector3 LocalVelocity => localVelocity;
        public Vector3 GroundNormal => groundNormal;
        public bool IsGrounded => isGrounded;

        public int Throttle { get; protected set; }
        public float Steer { get; protected set; }
        public bool Sprint { get; protected set; }
        public bool Brake { get; protected set; }

        public float SteerValue => steerValue;

        private bool isKinematic = false;
        protected bool IsKinematic
        {
            get => isKinematic;
            set
            {
                if (isKinematic != value)
                {
                    if (!value && rigidbody.isKinematic)
                    {
                        rigidbody.isKinematic = false;
                        rigidbody.WakeUp();
                    }
                    isKinematic = value;
                }
            }
        }


        protected virtual void Start()
        {
            physicMaterial = new PhysicMaterial("Vehicle Physic Material");
            physicMaterial.hideFlags |= HideFlags.NotEditable;
            physicMaterial.staticFriction = 0;
            physicMaterial.dynamicFriction = 0;
            physicMaterial.bounciness = 0;
            physicMaterial.frictionCombine = PhysicMaterialCombine.Multiply;
            physicMaterial.bounceCombine = PhysicMaterialCombine.Average;
            collider.sharedMaterial = physicMaterial;
        }

        private void FixedUpdate()
        {
            //debugText.Clear();

            // Update normals
            isGrounded = contactNormals.Count > 0;
            if (isGrounded)
            {
                groundNormal = Vector3.zero;
                for (int i = 0; i < contactNormals.Count; i++)
                {
                    groundNormal += contactNormals[i];
                }
                groundNormal.Normalize();

                contactNormals.Clear();
            }

            if (rigidbody.IsSleeping())
            {
                return;
            }

            float deltaTime = Time.deltaTime;

            steerValue = Mathf.MoveTowards(steerValue, Steer, 10 * deltaTime);

            // Rotate
            float rotateDirection = Mathf.Clamp(localVelocity.z, -1, 1);
            float rotatePerSecond = steerValue * steerAngle * rotateDirection * deltaTime;
            float yaw = transform.eulerAngles.y + rotatePerSecond;
            transform.eulerAngles = new Vector3(0, yaw, 0);

            // Update velocity
            localVelocity = transform.InverseTransformDirection(rigidbody.velocity);
            if (isGrounded)
            {
                // Apply sidewards grip
                localVelocity.x = Mathf.MoveTowards(localVelocity.x, 0, tyreFriction * deltaTime);
                float grip = 1 - Mathf.Clamp01(Mathf.Abs(localVelocity.x) / defaultSpeed);

                if (Brake)
                {
                    // Apply handbrake
                    localVelocity.z = Mathf.MoveTowards(localVelocity.z, 0, tyreFriction * deltaTime);
                }
                else if (Throttle > 0)
                {
                    // Forward accelerate
                    float max = Sprint ? maxSpeed : defaultSpeed;
                    float defaultDif = Mathf.Clamp((max - localVelocity.z) * defaultSpeed, -1, 1);
                    if (defaultDif > 0)
                    {
                        Vector3 normal = transform.InverseTransformDirection(groundNormal);
                        Vector3 localSurfaceForward = Mathfx.ProjectAlongSurface(Vector2.up, normal);
                        localVelocity += localSurfaceForward * forwardAcceleration * grip * defaultDif * deltaTime;
                    }
                    else
                        localVelocity.z += GetRollout(localVelocity.z) * defaultDif * deltaTime;
                }
                else if (Throttle < 0)
                {
                    if (localVelocity.z > 0.01f || Mathf.Abs(localVelocity.x) > 0.1f)
                    {
                        // Apply handbrake
                        localVelocity.z = Mathf.MoveTowards(localVelocity.z, 0, tyreFriction * deltaTime);
                    }
                    else
                    {
                        // Reverse
                        float defaultDif = Mathf.Clamp((-reverseSpeed - localVelocity.z) * defaultSpeed, -1, 1);
                        localVelocity.z += reverseAcceleration * grip * defaultDif * deltaTime;
                    }
                }
                else
                {
                    // Simple rollout
                    float rolloutVel = localVelocity.z > 0 ? GetRollout(localVelocity.z) : reverseAcceleration;
                    localVelocity.z = Mathf.MoveTowards(localVelocity.z, 0, rolloutVel * deltaTime);
                }
                rigidbody.velocity = transform.TransformDirection(localVelocity);
            }

            if (isKinematic && !rigidbody.isKinematic && localVelocity.sqrMagnitude < 0.01f)
            {
                rigidbody.isKinematic = true;
                rigidbody.Sleep();
            }

            //LogValue("vel.z", localVelocity.z);
            //LogValue("vel.x", localVelocity.x);
            //LogValue("vel.mag", localVelocity.magnitude);
        }


        /*private System.Text.StringBuilder debugText = new System.Text.StringBuilder();
        private void LogValue(string name, object value)
        {
            debugText.Append($"{name} = {value}\n");
        }
        private void LogValue(string name, float value)
        {
            debugText.Append($"{name} = {Mathf.Round(value * 10000) / 10000}\n");
        }
        private void OnGUI()
        {
            string text = debugText.ToString();
            GUI.color = Color.black;
            GUI.Label(new Rect(1, 1, Screen.width, Screen.height), text);
            GUI.color = Color.white;
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), text);
        }*/


        protected virtual void OnCollisionEnter(Collision collision)
        {
            OnCollision(collision, true);
        }
        protected virtual void OnCollisionStay(Collision collision)
        {
            OnCollision(collision, false);
        }
        protected virtual void OnCollisionExit(Collision collision)
        {

        }

        private void OnCollision(Collision collision, bool wasEnter)
        {
            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                if (contact.otherCollider.gameObject.layer != 0) continue;

                float y = contact.normal.y;
                if (y > 0)
                {
                    if (y > 1) y = 1;
                    float normalAngle = Mathf.Acos(y) * Mathf.Rad2Deg;
                    if (normalAngle <= maxGroundAngle)
                    {
                        contactNormals.Add(contact.normal);
                    }
                }
            }
        }


        public override void Teleport(Vector3 position, Quaternion rotation)
        {
            base.Teleport(position, rotation);

            rigidbody.velocity = localVelocity = Vector3.zero;
        }


#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (defaultSpeed > maxSpeed) defaultSpeed = maxSpeed;

            float halfHeight = collisionHeight * 0.5f;
            if (collisionRadius > halfHeight) collisionRadius = halfHeight;

            if (!Application.isPlaying)
            {
                if (!rigidbody)
                {
                    rigidbody = GetComponent<Rigidbody>();
                    UnityEditor.EditorUtility.SetDirty(this);
                }
                if (rigidbody)
                {
                    rigidbody.hideFlags |= HideFlags.NotEditable;
                    rigidbody.mass = 1;
                    rigidbody.drag = 0;
                    rigidbody.angularDrag = 0;
                    rigidbody.useGravity = true;
                    rigidbody.isKinematic = false;
                    rigidbody.interpolation = RigidbodyInterpolation.None;
                    rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                    UnityEditor.EditorUtility.SetDirty(this);
                }

                if (!collider)
                {
                    collider = GetComponent<CapsuleCollider>();
                    UnityEditor.EditorUtility.SetDirty(this);
                }
                if (collider)
                {
                    collider.hideFlags |= HideFlags.NotEditable;
                    collider.center = new Vector3(0, collisionHeight * 0.5f, 0);
                    collider.radius = collisionRadius;
                    collider.height = collisionHeight;
                    collider.direction = 1;
                    collider.sharedMaterial = null;
                }
            }
        }
#endif
    }
}
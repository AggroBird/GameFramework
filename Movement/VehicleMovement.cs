using AggroBird.UnityEngineExtend;
using System.Collections.Generic;
using UnityEngine;

namespace AggroBird.GameFramework
{
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public class VehicleMovement : Movement
    {
        [SerializeField, HideInInspector]
        protected new Rigidbody rigidbody = default;
        public Rigidbody Rigidbody => rigidbody;
        [SerializeField, HideInInspector]
        protected new CapsuleCollider collider = default;
        public Collider Collider => collider;

        [Header("Bicycle Settings")]
        [SerializeField, Clamped(min: 0.1f)] protected float collisionRadius = 0.5f;
        [SerializeField, Clamped(min: 0.2f)] protected float collisionHeight = 2;
        [Space]
        [SerializeField, Min(0)] protected float defaultSpeed = 4;
        [SerializeField, Min(0)] protected float maxSpeed = 4;
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
        [SerializeField] protected AnimationCurve rolloutCurve = new(new Keyframe { time = 0, value = 0.1f, }, new Keyframe { time = 1, value = 2, });
        [Space]
        [SerializeField, Min(0.1f)] protected float suspensionHeight = 0.5f;
        [SerializeField, Min(0)] protected float springForce = 250;
        [SerializeField, Min(0)] protected float springDamp = 30;
        [SerializeField, Min(0.1f)] protected float vehicleMass = 5;
        [SerializeField, Min(0.1f)] protected float wheelMass = 1;
        [SerializeField] protected LayerMask suspensionLayerMask = ~0;

        private float wheelPosition = 0;
        private float wheelVelocity = 0;


        private float GetRollout(float velz) => maxSpeed > 0 ? rolloutCurve.Evaluate(Mathf.Abs(velz) / maxSpeed) : 0;

        private PhysicMaterial physicMaterial = default;

        public float DefaultSpeed => defaultSpeed;
        public float MaxSpeed => maxSpeed;

        private Vector3 localVelocity = Vector3.zero;
        private float steerValue = 0;
        public float SteerValue => steerValue;

        private readonly List<Vector3> contactNormals = new();
        private Vector3 groundNormal = Vector3.up;
        private bool isGrounded = false;

        public Vector3 Velocity
        {
            get
            {
                return rigidbody.velocity;
            }
            set
            {
                rigidbody.velocity = value;
                localVelocity = transform.InverseTransformDirection(value);
            }
        }
        public Vector3 LocalVelocity => localVelocity;
        public Vector3 GroundNormal => groundNormal;
        public bool IsGrounded => isGrounded;

        public float ColliderRadius => collisionRadius;
        public float ColliderHeight => collisionHeight;
        public Vector3 BottomPosition
        {
            get
            {
                Vector3 position = transform.position;
                return new Vector3(position.x, wheelPosition - collisionRadius, position.z);
            }
        }
        public Vector3 Center => transform.position + collider.center;

        public int Throttle { get; set; }
        public float Steer { get; set; }
        public bool Sprint { get; set; }
        public bool Brake { get; set; }

        private bool isKinematic = false;
        public bool IsKinematic
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

            wheelPosition = transform.position.y + collisionRadius;
        }

        private void FixedUpdate()
        {
            //debugText.Clear();

            float deltaTime = Time.deltaTime;

            // Update suspension physics
            Vector3 position = transform.position;
            float wheelOrigin = position.y + collisionRadius + suspensionHeight;
            float distance = Mathf.Abs(wheelOrigin - wheelPosition);
            float force = -(distance - suspensionHeight) * springForce;
            float damp = (rigidbody.velocity.y - wheelVelocity) * springDamp;

            rigidbody.velocity += Vector3.up * ((force - damp) / rigidbody.mass * deltaTime);
            wheelVelocity += (-force + damp) / wheelMass * deltaTime;
            wheelVelocity += Physics.gravity.y * deltaTime;
            wheelPosition += wheelVelocity * deltaTime;
            wheelPosition = Mathf.Min(wheelPosition, wheelOrigin - 0.001f);

            // Update ground normals
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

            // Raycast wheel collision
            float originEpsilon = wheelOrigin + 0.001f;
            if (Physics.SphereCast(new Vector3(position.x, originEpsilon, position.z), collisionRadius, Vector3.down, out RaycastHit hit, Mathf.Abs(originEpsilon - wheelPosition), suspensionLayerMask))
            {
                float y = hit.normal.y;
                if (y > 0)
                {
                    if (y > 1) y = 1;
                    float normalAngle = Mathf.Acos(y) * Mathf.Rad2Deg;
                    if (normalAngle <= maxGroundAngle)
                    {
                        isGrounded = true;
                        groundNormal = hit.normal;
                        wheelPosition = hit.point.y + groundNormal.y * collisionRadius;
                        wheelVelocity *= -0.8f;
                    }
                }
            }

            if (rigidbody.IsSleeping())
            {
                return;
            }

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
                        localVelocity += localSurfaceForward * (forwardAcceleration * grip * defaultDif * deltaTime);
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
            OnCollision(collision);
        }
        protected virtual void OnCollisionStay(Collision collision)
        {
            OnCollision(collision);
        }
        protected virtual void OnCollisionExit(Collision collision)
        {

        }

        private void OnCollision(Collision collision)
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
            wheelPosition = transform.position.y + collisionRadius;

            if (defaultSpeed > maxSpeed) maxSpeed = defaultSpeed;

            float halfHeight = collisionHeight * 0.5f;
            if (collisionRadius > halfHeight) collisionRadius = halfHeight;

            if (Utility.EnsureComponentReference(this, ref rigidbody))
            {
                rigidbody.hideFlags |= HideFlags.NotEditable;
                rigidbody.mass = vehicleMass;
                rigidbody.drag = 0;
                rigidbody.angularDrag = 0;
                rigidbody.useGravity = true;
                rigidbody.isKinematic = false;
                rigidbody.interpolation = RigidbodyInterpolation.None;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }

            if (Utility.EnsureComponentReference(this, ref collider))
            {
                collider.hideFlags |= HideFlags.NotEditable;
                collider.center = new Vector3(0, collisionHeight * 0.5f + suspensionHeight, 0);
                collider.radius = collisionRadius;
                collider.height = collisionHeight;
                collider.direction = 1;
                collider.sharedMaterial = null;
            }
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.white;
            Vector3 position = transform.position;
            if (Application.isPlaying)
            {
                Gizmos.DrawWireSphere(new Vector3(position.x, wheelPosition, position.z), collisionRadius);
            }
            else
            {
                Gizmos.DrawWireSphere(new Vector3(position.x, position.y + collisionRadius, position.z), collisionRadius);
            }
        }
#endif
    }
}
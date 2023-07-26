using AggroBird.UnityExtend;
using System.Collections.Generic;
using UnityEngine;

namespace AggroBird.GameFramework
{
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public class LegacyVehicleMovement : Movement
    {
        [SerializeField, HideInInspector]
        protected new Rigidbody rigidbody = default;
        public Rigidbody Rigidbody => rigidbody;
        [SerializeField, HideInInspector]
        protected new CapsuleCollider collider = default;
        public Collider Collider => collider;

        [Header("Bicycle Settings")]
        [SerializeField, Clamped(min: 0.1f)] protected float collisionRadius = 0.5f;
        [SerializeField, Clamped(min: 0.2f)] protected float collisionHeight = 1.75f;
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
        [SerializeField, Min(0.1f)] protected float vehicleMass = 1;
        [SerializeField, Min(0)] protected float suspensionHeight = 0.25f;
        [SerializeField, Min(0)] protected float springConstant = 100;
        [SerializeField, Min(0)] protected float damperConstant = 10;
        [SerializeField] protected LayerMask suspensionLayerMask = ~0;
        [Space]
        [SerializeField, Clamped(0, 1)] protected float staticFriction = 0.6f;
        [SerializeField, Clamped(0, 1)] protected float dynamicFriction = 0.6f;


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
        public Vector3 GroundPosition
        {
            get
            {
                Vector3 position = transform.position;
                return new Vector3(position.x, position.y + suspensionHeight - distanceToGround, position.z);
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

        private float previousSuspensionDistance;
        private float currentSuspensionDistance;
        private float springVelocity;
        private float springForce;
        private float damperForce;
        private float distanceToGround;
        private readonly RaycastHit[] hits = new RaycastHit[32];


        protected virtual void Start()
        {
            physicMaterial = new PhysicMaterial("Vehicle Physic Material");
            physicMaterial.hideFlags |= HideFlags.NotEditable;
            physicMaterial.staticFriction = staticFriction;
            physicMaterial.dynamicFriction = dynamicFriction;
            physicMaterial.bounciness = 0;
            physicMaterial.frictionCombine = PhysicMaterialCombine.Average;
            physicMaterial.bounceCombine = PhysicMaterialCombine.Average;
            collider.sharedMaterial = physicMaterial;

            distanceToGround = suspensionHeight;
        }

        private void FixedUpdate()
        {
            //debugText.Clear();

            float deltaTime = Time.deltaTime;

            Vector3 position = rigidbody.position;
            float wheelOrigin = position.y + collisionRadius + suspensionHeight;

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

            int hitCount = Physics.SphereCastNonAlloc(new Vector3(position.x, wheelOrigin, position.z), collisionRadius - 0.0001f, Vector3.down, hits, suspensionHeight, suspensionLayerMask);
            if (hitCount > 0)
            {
                float smallestDist = float.MaxValue;
                for (int i = 0; i < hitCount; i++)
                {
                    ref RaycastHit hit = ref hits[i];
                    if (!ReferenceEquals(hit.collider, collider) && hit.distance < smallestDist)
                    {
                        float y = hit.normal.y;
                        if (y >= 0)
                        {
                            if (y > 1) y = 1;
                            float normalAngle = Mathf.Acos(y) * Mathf.Rad2Deg;
                            if (normalAngle <= maxGroundAngle)
                            {
                                smallestDist = hit.distance;
                                groundNormal = hit.normal;
                            }
                        }
                    }
                }
                if (smallestDist < float.MaxValue)
                {
                    distanceToGround = smallestDist;
                    // Hooke's Law
                    previousSuspensionDistance = currentSuspensionDistance;
                    currentSuspensionDistance = suspensionHeight - smallestDist;
                    springVelocity = (currentSuspensionDistance - previousSuspensionDistance) / deltaTime;
                    springForce = springConstant * currentSuspensionDistance;
                    damperForce = damperConstant * springVelocity;
                    rigidbody.AddForceAtPosition(new Vector3(0, springForce + damperForce, 0), transform.position, ForceMode.Force);

                    isGrounded = true;
                }
            }

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
                rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
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
                Gizmos.DrawWireSphere(GroundPosition + new Vector3(0, collisionRadius, 0), collisionRadius);
            }
            else
            {
                Gizmos.DrawWireSphere(new Vector3(position.x, position.y + collisionRadius, position.z), collisionRadius);
            }
        }
#endif
    }
}
using AggroBird.UnityExtend;
using System.Collections.Generic;
using UnityEngine;

namespace AggroBird.GameFramework
{
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public class LegacyCharacterMovement : Movement
    {
        public enum MovementState
        {
            Grounded = 0,
            Sliding,
            Falling,
            Jumping,
        }

        [SerializeField, HideInInspector]
        [UnityEngine.Serialization.FormerlySerializedAs("rigidbodyComponent")]
        private new Rigidbody rigidbody = default;
        public Rigidbody Rigidbody => rigidbody;
        [SerializeField, HideInInspector]
        [UnityEngine.Serialization.FormerlySerializedAs("colliderComponent")]
        private new CapsuleCollider collider = default;
        public CapsuleCollider Collider => collider;

        [Header("Character Settings")]
        [SerializeField] protected Space inputSpace = Space.World;
        [SerializeField] protected bool rotateTowardsInput = false;
        [SerializeField, Clamped(min: 0.1f)] protected float rotationSpeed = 100;
        [Space]
        [SerializeField, Clamped(min: 0.1f)] protected float collisionRadius = 0.5f;
        [SerializeField, Clamped(min: 0.2f)] protected float collisionHeight = 2;
        [Space]
        [SerializeField, Clamped(min: 0.01f)] protected float walkSpeed = 7;
        [SerializeField, Clamped(min: 0.01f)] protected float walkAcceleration = 21;
        [SerializeField, Clamped(min: 0.01f)] protected float fallMaxMoveSpeed = 3;
        [SerializeField, Clamped(min: 0.01f)] protected float fallMoveAcceleration = 5;
        [Space]
        [SerializeField] protected bool canJump = true;
        [SerializeField, Clamped(min: 0)] protected float jumpForce = 5;
        [Space]
        [SerializeField, Clamped(min: 0, max: 90)] protected float maxGroundAngle = 50;
        [SerializeField, Clamped(min: 0)] protected float gravityScale = 1;

        // Input properties
        public Vector2 MovementInput { get; set; }
        public bool IsKinematic
        {
            get
            {
                return rigidbody.isKinematic;
            }
            set
            {
                if (value)
                {
                    rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
                    rigidbody.isKinematic = value;
                }
                else
                {
                    rigidbody.isKinematic = value;
                    rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                }
            }
        }

        protected virtual float MovementSpeedModifier => 1;
        protected virtual float JumpForceModifier => 1;
        protected bool useAcceleration = true;

        private float GroundedPushForce => Physics.gravity.magnitude;
        private const float GroundedCorrectionDuration = 0.1f;

        public float MaxWalkSpeed => walkSpeed;
        public float EffectiveWalkSpeed => walkSpeed * MovementSpeedModifier;

        // Private movement variables
        public MovementState State => groundedCorrectionTime > 0 ? MovementState.Grounded : state;
        private MovementState state = MovementState.Falling;
        public bool IsGrounded => State == MovementState.Grounded;
        private Vector3 groundNormal = Vector3.up;
        private PhysicMaterial physicMaterial = default;
        private float groundedCorrectionTime = 0;

        public Vector3 Velocity
        {
            get
            {
                return rigidbody.velocity;
            }
            set
            {
                rigidbody.velocity = value;
            }
        }
        public Vector2 HorizontalVelocity
        {
            get
            {
                Vector3 vel = Velocity;
                return new Vector2(vel.x, vel.z);
            }
            set
            {
                Velocity = new Vector3(value.x, Velocity.y, value.y);
            }
        }
        public float VerticalVelocity
        {
            get
            {
                return Velocity.y;
            }
            set
            {
                Vector3 vel = Velocity;
                Velocity = new Vector3(vel.x, value, vel.z);
            }
        }

        public float ColliderRadius => collisionRadius;
        public float ColliderHeight => collisionHeight;
        public Vector3 BottomPosition
        {
            get
            {
                Vector3 position = transform.position;
                position.y = (transform.position.y + collider.center.y) - ColliderHeight * 0.5f;
                return position;
            }
        }
        public Vector3 Center => transform.position + collider.center;

        private readonly List<Vector3> walkableNormals = new();
        private readonly List<Vector3> slopeNormals = new();
        private float currentFriction = 0;


        protected virtual void Awake()
        {
            transform.SetParent(null);
            transform.localScale = Vector3.one;
            transform.eulerAngles = new Vector3(0, transform.eulerAngles.y, 0);

            if (!collider) collider = GetComponent<CapsuleCollider>();
            if (!rigidbody) rigidbody = GetComponent<Rigidbody>();

            state = MovementState.Falling;

            physicMaterial = new PhysicMaterial();
            physicMaterial.dynamicFriction = physicMaterial.staticFriction = physicMaterial.bounciness = 0;
            physicMaterial.frictionCombine = PhysicMaterialCombine.Minimum;
            physicMaterial.bounceCombine = PhysicMaterialCombine.Minimum;
            collider.sharedMaterial = physicMaterial;
        }

        protected virtual void FixedUpdate()
        {
            float delta = Time.deltaTime;

            if (walkableNormals.Count > 0)
            {
                groundNormal = Normalize(walkableNormals);

                // If we are jumping, make sure we don't get grounded immediately after taking off
                Vector3 velocityNormal = rigidbody.velocity.normalized;
                if (!(state == MovementState.Jumping && Vector3.Dot(velocityNormal, groundNormal) > 0.01f))
                {
                    state = MovementState.Grounded;
                    groundedCorrectionTime = GroundedCorrectionDuration;
                }
            }
            else if (slopeNormals.Count > 0)
            {
                groundNormal = Normalize(slopeNormals);

                state = MovementState.Sliding;
            }
            else
            {
                state = MovementState.Falling;
            }

            walkableNormals.Clear();
            slopeNormals.Clear();

            if (groundedCorrectionTime > 0) groundedCorrectionTime -= delta;

            Vector2 moveInput = MovementInput;
            float moveInputLength = moveInput.magnitude;
            if (moveInputLength > 1)
            {
                moveInput /= moveInputLength;
                moveInputLength = 1;
            }

            if (inputSpace == Space.Self)
            {
                moveInput = Mathfx.RotateDeg(moveInput, transform.eulerAngles.y);
            }

            bool hasInput = moveInputLength >= Mathf.Epsilon;
            if (rigidbody.IsSleeping() && !hasInput)
            {
                return;
            }

            // Update velocity
            bool moving = false;
            if (IsGrounded)
            {
                Vector3 targetSpeed = Vector3.zero;

                if (hasInput)
                {
                    // Project along ground normal
                    targetSpeed = ProjectAlongSurface(moveInput, groundNormal) * EffectiveWalkSpeed;
                    moving = true;
                }

                Vector3 newVelocity = useAcceleration ? Vector3.MoveTowards(rigidbody.velocity, targetSpeed, walkAcceleration * delta) : targetSpeed;
                if (newVelocity.sqrMagnitude < 0.01f && !hasInput)
                {
                    rigidbody.velocity = Vector3.zero;
                    rigidbody.Sleep();
                }
                else
                {
                    rigidbody.velocity = newVelocity + groundNormal * -(GroundedPushForce * delta);
                    if (rotateTowardsInput && hasInput)
                    {
                        rigidbody.rotation = Quaternion.RotateTowards(rigidbody.rotation, Quaternion.LookRotation(moveInput.Horizontal3D(), Vector3.up), rotationSpeed * delta);
                    }
                }
            }
            else if (state == MovementState.Falling || state == MovementState.Jumping)
            {
                if (moveInput.sqrMagnitude > 0 && fallMoveAcceleration > 0)
                {
                    Vector2 horizontal = HorizontalVelocity;
                    float currentMagnitude = horizontal.magnitude;
                    float forceStrength = 1;
                    float forceMax = fallMaxMoveSpeed * MovementSpeedModifier;
                    if (currentMagnitude >= forceMax)
                    {
                        forceStrength = Mathf.Clamp01(-Vector2.Dot(horizontal.normalized, moveInput.normalized));
                        forceMax = currentMagnitude;
                    }

                    if (forceStrength > 0)
                    {
                        horizontal += moveInput * (fallMoveAcceleration * forceStrength * delta);
                        if (horizontal.magnitude > forceMax)
                        {
                            horizontal = horizontal.normalized * forceMax;
                        }

                        HorizontalVelocity = horizontal;
                    }
                }
            }

            if (rigidbody.useGravity && !IsGrounded)
            {
                // Apply gravity scale
                float relativeGravityScale = gravityScale < 1 ? -(1 - gravityScale) : (gravityScale - 1);
                rigidbody.velocity += Physics.gravity * (relativeGravityScale * delta);
            }

            // Update friction
            float setFriction = IsGrounded && !moving ? 1 : 0;
            if (setFriction != currentFriction)
            {
                physicMaterial.dynamicFriction = physicMaterial.staticFriction = currentFriction = setFriction;
            }
        }

        public void Jump()
        {
            if (canJump && IsGrounded)
            {
                Jump(jumpForce * JumpForceModifier);
            }
        }
        protected void Jump(float setVerticalVelocity, bool additive = false)
        {
            Vector3 currentVel = Velocity;
            Jump(new Vector3(currentVel.x, setVerticalVelocity, currentVel.z), additive);
        }
        protected void Jump(Vector3 setVelocity, bool additive = false)
        {
            rigidbody.velocity = additive ? (rigidbody.velocity + setVelocity) : setVelocity;
            state = MovementState.Jumping;
            groundedCorrectionTime = 0;
            OnJump();
        }
        protected virtual void OnJump()
        {

        }


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
                    ((normalAngle <= maxGroundAngle) ? walkableNormals : slopeNormals).Add(contact.normal);
                }
            }
        }

        private static Vector3 Normalize(List<Vector3> list)
        {
            Vector3 normal = Vector3.zero;
            for (int i = 0; i < list.Count; i++)
            {
                normal += list[i];
            }
            normal /= list.Count;
            return normal.normalized;
        }
        private static Vector3 ProjectAlongSurface(Vector2 dir, Vector3 normal)
        {
            float len = dir.magnitude;
            if (len > Mathf.Epsilon)
            {
                Vector3 perp = Vector3.Cross(dir.Horizontal3D(), Vector3.up);
                return Vector3.Cross(normal, perp).normalized * dir.magnitude;
            }
            return Vector3.zero;
        }


        public override void Teleport(Vector3 position, Quaternion rotation)
        {
            base.Teleport(position, rotation);

            rigidbody.velocity = Vector3.zero;
        }


#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            float halfHeight = collisionHeight * 0.5f;
            if (collisionRadius > halfHeight) collisionRadius = halfHeight;

            if (Utility.EnsureComponentReference(this, ref rigidbody))
            {
                rigidbody.hideFlags |= HideFlags.NotEditable;
                rigidbody.mass = 1;
                rigidbody.drag = 0;
                rigidbody.angularDrag = 0.05f;
                rigidbody.useGravity = true;
                rigidbody.isKinematic = false;
                rigidbody.interpolation = RigidbodyInterpolation.None;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            }

            if (Utility.EnsureComponentReference(this, ref collider))
            {
                collider.hideFlags |= HideFlags.NotEditable;
                collider.radius = collisionRadius;
                collider.height = collisionHeight;
                collider.enabled = true;
                collider.isTrigger = false;
                collider.center = new Vector3(0, collisionHeight * 0.5f, 0);
                collider.direction = 1;
            }
        }
#endif
    }
}
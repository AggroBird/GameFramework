using AggroBird.UnityEngineExtend;
using UnityEngine;

namespace AggroBird.GameFramework
{
    // Based on physics based character controller by Toyful games https://www.toyfulgames.com/
    public class CharacterMovement : Movement
    {
        private enum LookDirectionOptions
        {
            Velocity,
            Input,
            Manual,
        };

        [SerializeField, HideInInspector]
        private new Rigidbody rigidbody = default;
        public Rigidbody Rigidbody => rigidbody;
        [SerializeField, HideInInspector]
        private new CapsuleCollider collider = default;
        public CapsuleCollider Collider => collider;

        [Header("Settings")]
        [SerializeField] private LookDirectionOptions characterLookDirection = LookDirectionOptions.Velocity;
        [SerializeField, Min(0)] private float rotateSpeed = 500;
        [SerializeField, Min(0.1f)] private float collisionRadius = 0.5f;
        [SerializeField, Min(0.2f)] private float collisionHeight = 1.75f;

        [Header("Suspension")]
        [SerializeField, Min(0)] private float suspensionHeight = 0.25f;
        [SerializeField, Min(0)] private float rayExtend = 0.25f;
        [SerializeField, Min(0)] private float suspensionSpringStrength = 200;
        [SerializeField, Min(0)] private float suspensionSpringDamper = 20;
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("terrainLayer")] private LayerMask suspensionLayerMask;

        [Header("Movement")]
        [SerializeField, Min(0)] private float maxSpeed = 6;
        [SerializeField, Min(0)] private float acceleration = 400;
        [SerializeField, Min(0)] private float maxAccelForce = 300;
        [SerializeField] private AnimationCurve accelerationFactorFromDot;
        [SerializeField] private AnimationCurve maxAccelerationForceFactorFromDot;

        [Header("Jump")]
        [SerializeField, Min(0)] private float jumpForceFactor = 16;
        [SerializeField, Min(0)] private float riseGravityFactor = 3;
        [SerializeField, Min(0)] private float fallGravityFactor = 8;
        [SerializeField, Min(0)] private float lowJumpFactor = 10;
        [SerializeField, Min(0)] private float jumpBuffer = 0.15f;
        [SerializeField, Min(0)] private float coyoteTime = 0.25f;


        public Vector2 MovementInput { get; set; }
        public bool Jump
        {
            get
            {
                return jumpInput;
            }
            set
            {
                if (value != jumpInput)
                {
                    jumpInput = value;

                    if (value)
                    {
                        jumpInputTime = Time.fixedTime;
                    }
                }
            }
        }
        private bool jumpInput = false;

        private Vector3 gravitationalForce;
        private static PhysicMaterial physicMaterial = default;

        private bool shouldMaintainHeight = true;

        private float speedFactor = 1f;
        private float maxAccelForceFactor = 1f;
        private Vector3 goalVelocity = Vector3.zero;

        private float? jumpInputTime = null;
        private float timeSinceUngrounded = 0f;
        private float timeSinceJump = 0f;
        private bool jumpReady = true;
        private bool isJumping = false;
        private bool isGrounded = false;

        public float ColliderRadius => collisionRadius;
        public float ColliderHeight => collisionHeight;
        public Vector3 Center
        {
            get
            {
                Vector3 position = transform.position;
                position.y += (suspensionHeight + collisionHeight) * 0.5f;
                return position;
            }
        }
        public bool IsGrounded => isGrounded;

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
                return Velocity.GetXZ();
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
                    rigidbody.isKinematic = true;
                    rigidbody.velocity = Vector3.zero;
                }
                else
                {
                    rigidbody.isKinematic = false;
                    rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                }
            }
        }


        protected virtual void Start()
        {
            rigidbody = GetComponent<Rigidbody>();
            gravitationalForce = Physics.gravity * rigidbody.mass;

            if (!physicMaterial)
            {
                physicMaterial = new PhysicMaterial("Character Physic Material");
                physicMaterial.hideFlags |= HideFlags.NotEditable;
                physicMaterial.bounciness = physicMaterial.dynamicFriction = physicMaterial.staticFriction = 0;
                physicMaterial.frictionCombine = PhysicMaterialCombine.Average;
                physicMaterial.bounceCombine = PhysicMaterialCombine.Average;
            }
            collider.sharedMaterial = physicMaterial;
        }

        protected virtual void FixedUpdate()
        {
            if (!rigidbody.isKinematic)
            {
                // Get normalized input
                Vector2 input = MovementInput;
                float len = input.magnitude;
                if (len > 1)
                {
                    input /= len;
                }

                bool rayHitGround = Physics.Raycast(new Ray(transform.position + new Vector3(0, suspensionHeight + 0.0001f, 0), Vector3.down), out RaycastHit hit, suspensionHeight + rayExtend, suspensionLayerMask.value);
                isGrounded = rayHitGround && hit.distance <= suspensionHeight * 1.3f;
                if (isGrounded)
                {
                    timeSinceUngrounded = 0f;

                    if (timeSinceJump > 0.2f)
                    {
                        isJumping = false;
                        jumpReady = true;
                    }
                }
                else
                {
                    timeSinceUngrounded += Time.fixedDeltaTime;
                }

                CharacterMove(input);
                CharacterJump(hit);

                if (rayHitGround && shouldMaintainHeight)
                {
                    MaintainHeight(hit);
                }

                UpdateLookDirection();
            }
        }

        private void MaintainHeight(RaycastHit rayHit)
        {
            Vector3 vel = rigidbody.velocity;
            Vector3 otherVel = Vector3.zero;
            Rigidbody hitBody = rayHit.rigidbody;
            if (hitBody != null)
            {
                otherVel = hitBody.velocity;
            }
            float rayDirVel = Vector3.Dot(Vector3.down, vel);
            float otherDirVel = Vector3.Dot(Vector3.down, otherVel);

            float relVel = rayDirVel - otherDirVel;
            float currHeight = rayHit.distance - suspensionHeight;
            float springForce = (currHeight * suspensionSpringStrength) - (relVel * suspensionSpringDamper);
            Vector3 maintainHeightForce = -gravitationalForce + springForce * Vector3.down;
            rigidbody.AddForce(maintainHeightForce);
        }

        private void CharacterMove(Vector2 moveInput)
        {
            Vector3 unitGoal = moveInput.Horizontal3D();
            Vector3 unitVel = goalVelocity.normalized;
            float velDot = Vector3.Dot(unitGoal, unitVel);
            float accel = acceleration * accelerationFactorFromDot.Evaluate(velDot);
            Vector3 goalVel = unitGoal * maxSpeed * speedFactor;
            goalVelocity = Vector3.MoveTowards(goalVelocity, goalVel, accel * Time.fixedDeltaTime);
            Vector3 neededAccel = (goalVelocity - rigidbody.velocity) / Time.fixedDeltaTime;
            float maxAccel = maxAccelForce * maxAccelerationForceFactorFromDot.Evaluate(velDot) * maxAccelForceFactor;
            neededAccel = Vector3.ClampMagnitude(neededAccel, maxAccel);
            rigidbody.AddForce(Vector3.Scale(neededAccel * rigidbody.mass, new Vector3(1, 0, 1)));
        }

        private void CharacterJump(RaycastHit rayHit)
        {
            timeSinceJump += Time.fixedDeltaTime;
            if (rigidbody.velocity.y < 0)
            {
                shouldMaintainHeight = true;
                if (!isGrounded)
                {
                    // Increase downforce for a sudden plummet.
                    rigidbody.AddForce(gravitationalForce * (fallGravityFactor - 1f)); // Hmm... this feels a bit weird. I want a reactive jump, but I don't want it to dive all the time...
                }
            }
            else if (rigidbody.velocity.y > 0)
            {
                if (!isGrounded)
                {
                    if (isJumping)
                    {
                        rigidbody.AddForce(gravitationalForce * (riseGravityFactor - 1f));
                    }
                    if (!jumpInput)
                    {
                        // Impede the jump height to achieve a low jump.
                        rigidbody.AddForce(gravitationalForce * (lowJumpFactor - 1f));
                    }
                }
            }

            if (jumpInputTime.HasValue)
            {
                float timeSinceJumpPressed = Time.fixedTime - jumpInputTime.Value;
                if (timeSinceJumpPressed < jumpBuffer && timeSinceUngrounded < coyoteTime)
                {
                    if (jumpReady)
                    {
                        jumpReady = false;
                        shouldMaintainHeight = false;
                        isJumping = true;
                        rigidbody.velocity = new Vector3(rigidbody.velocity.x, 0f, rigidbody.velocity.z); // Cheat fix... (see comment below when adding force to rigidbody).
                        if (rayHit.distance != 0) // i.e. if the ray has hit
                        {
                            rigidbody.position = new Vector3(rigidbody.position.x, rigidbody.position.y - (rayHit.distance - suspensionHeight), rigidbody.position.z);
                        }
                        rigidbody.AddForce(Vector3.up * jumpForceFactor, ForceMode.Impulse); // This does not work very consistently... Jump height is affected by initial y velocity and y position relative to RideHeight... Want to adopt a fancier approach (more like PlayerMovement). A cheat fix to ensure consistency has been issued above...
                        jumpInputTime = null;
                        timeSinceJump = 0f;
                    }
                }
            }
        }

        private void UpdateLookDirection()
        {
            if (characterLookDirection != LookDirectionOptions.Manual)
            {
                float target;
                if (characterLookDirection == LookDirectionOptions.Velocity)
                {
                    Vector2 velocity = HorizontalVelocity;
                    if (velocity.sqrMagnitude > 0.001f)
                    {
                        target = Mathfx.AngleFromVectorDeg(velocity);
                    }
                    else
                    {
                        return;
                    }
                }
                else if (MovementInput.sqrMagnitude > 0.001f)
                {
                    target = Mathfx.AngleFromVectorDeg(MovementInput);
                }
                else
                {
                    return;
                }

                transform.SetYaw(Mathf.MoveTowardsAngle(transform.GetYaw(), target, rotateSpeed * Time.fixedDeltaTime));
            }
        }


#if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(transform.position, transform.position + new Vector3(0, suspensionHeight, 0));
        }
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
                collider.center = new Vector3(0, collisionHeight * 0.5f + suspensionHeight, 0);
                collider.direction = 1;
            }
        }
#endif
    }
}

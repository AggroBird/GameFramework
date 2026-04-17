using AggroBird.UnityExtend;
using UnityEngine;

namespace AggroBird.GameFramework
{
    public enum LookDirectionOptions
    {
        Velocity,
        Input,
        Manual,
    };

    // Based on physics based character controller by Toyful games https://www.toyfulgames.com/
    public class CharacterMovement : Movement
    {
        [SerializeField]
        private new Rigidbody rigidbody = default;
        public Rigidbody Rigidbody => rigidbody;
        [SerializeField]
        private new SphereCollider collider = default;
        public SphereCollider Collider => collider;

        [Header("Settings")]
        public LookDirectionOptions characterLookDirection = LookDirectionOptions.Velocity;
        [SerializeField, Min(0)] private float rotateSpeed = 500;

        [Header("Suspension")]
        [SerializeField, Min(0)] private float suspensionHeight = 0.25f;
        [SerializeField, Min(0)] private float rayExtend = 0.25f;
        [SerializeField, Min(0)] private float suspensionSpringStrength = 200;
        [SerializeField, Min(0)] private float suspensionSpringDamper = 20;
        [SerializeField] private LayerMask suspensionLayerMask;

        [Header("Movement")]
        [SerializeField, Min(0)] private float maxSpeed = 6;
        [SerializeField, Min(0)] private float sprintSpeed = 10;
        [SerializeField, Min(0)] private float acceleration = 400;
        [SerializeField, Min(0)] private float maxAccelForce = 300;
        [SerializeField] private AnimationCurve accelerationFactorFromDot;
        [SerializeField] private AnimationCurve maxAccelerationForceFactorFromDot;
        [SerializeField, Min(0)] private float gravityScale = 1;
        [SerializeField, Range(0, 90)] private float slopeAngleLimit = 45;

        [Header("Roll")]
        [SerializeField, Min(0)] private float rollDuration = 0.5f;
        [SerializeField, Min(0)] private float rollVelocity = 10;
        [SerializeField, Min(0)] private float rollCooldown = 0.5f;

        [Header("Sprint")]
        [SerializeField] private bool useStamina = false;
        [SerializeField, Min(0.0001f)] private float maxStamina = 10;
        [SerializeField, Min(0)] private float staminaRechargeRate = 1;


        protected virtual float MaxSpeedModifier => 1;

        public Vector2 MovementInput { get; set; }
        public bool Sprint { get; set; }

        public float MaxSpeed => maxSpeed;

        public float MaxStamina => maxStamina;
        public float Stamina { get; private set; }
        public float SprintSpeed => sprintSpeed;
        public bool UseStamina => useStamina;
        public bool IsSprinting => Sprint && (!useStamina || Stamina > 0);

        private static PhysicsMaterial physicMaterial = default;

        private Vector3 goalVelocity = Vector3.zero;

        private Vector2 rollDirection;
        private float rollProgress;
        public bool IsRolling { get; private set; }
        public bool CanRoll => Time.fixedTime - lastRollTime >= rollCooldown && isGrounded;
        private float lastRollTime = -999;
        private bool isGrounded = false;
        public LayerMask LayerMask => suspensionLayerMask;

        public Vector3 ColliderCenter
        {
            get
            {
                transform.GetPositionAndRotation(out Vector3 position, out Quaternion rotation);
                return position + rotation * collider.center;
            }
        }
        public bool IsGrounded => isGrounded;

        public Vector3 LinearVelocity
        {
            get
            {
                return rigidbody.linearVelocity;
            }
            set
            {
                if (!rigidbody.isKinematic)
                {
                    rigidbody.linearVelocity = value;
                }
            }
        }
        public Vector2 HorizontalVelocity
        {
            get
            {
                return LinearVelocity.GetXZ();
            }
            set
            {
                LinearVelocity = new Vector3(value.x, LinearVelocity.y, value.y);
            }
        }
        public float VerticalVelocity
        {
            get
            {
                return LinearVelocity.y;
            }
            set
            {
                Vector3 vel = LinearVelocity;
                LinearVelocity = new Vector3(vel.x, value, vel.z);
            }
        }
        public float GroundPeneration { get; private set; }

        public bool IsPhysicsEnabled => physicsDisableStack == 0;
        private int physicsDisableStack = 0;
        public void PushDisablePhysics()
        {
            if (physicsDisableStack == 0)
            {
                rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
                rigidbody.linearVelocity = Vector3.zero;
                rigidbody.isKinematic = true;
                goalVelocity = Vector3.zero;
                enabled = false;
            }
            physicsDisableStack++;
        }
        public void PopDisablePhysics()
        {
            if (physicsDisableStack == 1)
            {
                rigidbody.isKinematic = false;
                rigidbody.WakeUp();
                rigidbody.linearVelocity = Vector3.zero;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                enabled = true;
                physicsDisableStack = 0;
            }
            else if (physicsDisableStack == 0)
            {
                Debug.LogWarning("Popping more than pushing", this);
            }
            else
            {
                physicsDisableStack--;
            }
        }


        protected virtual void Start()
        {
            rigidbody = GetComponent<Rigidbody>();

            if (!physicMaterial)
            {
                physicMaterial = new PhysicsMaterial("Character Physic Material");
                physicMaterial.hideFlags |= HideFlags.NotEditable;
                physicMaterial.bounciness = physicMaterial.dynamicFriction = physicMaterial.staticFriction = 0;
                physicMaterial.frictionCombine = PhysicsMaterialCombine.Average;
                physicMaterial.bounceCombine = PhysicsMaterialCombine.Average;
            }
            collider.sharedMaterial = physicMaterial;

            Stamina = MaxStamina;
        }

        public RaycastHit TraceGround()
        {
            isGrounded = false;
            float sphereCastRadius = collider.radius * 0.5f;
            Vector3 colliderCenter = ColliderCenter;
            colliderCenter -= new Vector3(0, collider.radius - sphereCastRadius, 0);
            float len = suspensionHeight + rayExtend + sphereCastRadius;
            //DebugUtility.DrawWireSphere(colliderCenter, Quaternion.identity, sphereCastRadius, Color.magenta, 0.3f);
            //DebugUtility.DrawWireSphere(colliderCenter - new Vector3(0, len, 0), Quaternion.identity, sphereCastRadius, Color.magenta, 0.3f);
            if (Physics.SphereCast(colliderCenter, sphereCastRadius, Vector3.down, out RaycastHit hit, len, suspensionLayerMask.value, QueryTriggerInteraction.Ignore))
            {
                float normalAngle = Vector3.Angle(hit.normal, Vector3.up);
                if (normalAngle < slopeAngleLimit)
                {
                    isGrounded = true;
                }
            }
            GroundPeneration = isGrounded ? hit.distance - suspensionHeight : 0;
            return hit;
        }

        public virtual void UpdatePhysics()
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

                var hit = TraceGround();

                CharacterMove(input);

                if (isGrounded)
                {
                    MaintainHeight(hit);
                }

                UpdateLookDirection();
            }
        }
        private void FixedUpdate()
        {
            if (useStamina)
            {
                float deltaTime = Time.deltaTime;
                if (Sprint)
                {
                    Stamina -= deltaTime;
                    if (Stamina < 0)
                    {
                        Stamina = 0;
                    }
                }
                else
                {
                    Stamina += deltaTime * staminaRechargeRate;
                    if (Stamina > MaxStamina)
                    {
                        Stamina = MaxStamina;
                    }
                }
            }
        }

        private void MaintainHeight(RaycastHit hit)
        {
            Vector3 vel = rigidbody.linearVelocity;
            Vector3 otherVel = Vector3.zero;
            Rigidbody hitBody = hit.rigidbody;
            if (hitBody != null)
            {
                otherVel = hitBody.linearVelocity;
            }
            float rayDirVel = Vector3.Dot(Vector3.down, vel);
            float otherDirVel = Vector3.Dot(Vector3.down, otherVel);

            float relVel = rayDirVel - otherDirVel;
            float springForce = GroundPeneration * suspensionSpringStrength - relVel * suspensionSpringDamper;

            Vector3 gravitationalForce = Physics.gravity * (gravityScale * rigidbody.mass);
            Vector3 maintainHeightForce = -gravitationalForce + springForce * Vector3.down;
            rigidbody.AddForce(maintainHeightForce);
        }

        private void CharacterMove(Vector2 moveInput)
        {
            float deltaTime = Time.deltaTime;
            if (!IsRolling)
            {
                if (isGrounded)
                {
                    Vector3 unitGoal = moveInput.Horizontal3D();
                    Vector3 unitVel = goalVelocity.normalized;
                    float velDot = Vector3.Dot(unitGoal, unitVel);
                    float accel = acceleration * accelerationFactorFromDot.Evaluate(velDot);
                    Vector3 goalVel = unitGoal * ((IsSprinting ? sprintSpeed : maxSpeed) * MaxSpeedModifier);
                    goalVelocity = Vector3.MoveTowards(goalVelocity, goalVel, accel * deltaTime);
                    Vector3 neededAccel = (goalVelocity - rigidbody.linearVelocity) / deltaTime;
                    float maxAccel = maxAccelForce * maxAccelerationForceFactorFromDot.Evaluate(velDot);
                    neededAccel = Vector3.ClampMagnitude(neededAccel, maxAccel);
                    rigidbody.AddForce(Vector3.Scale(neededAccel * rigidbody.mass, new Vector3(1, 0, 1)));
                }
            }
            else
            {
                Vector3 vel = rigidbody.linearVelocity;
                vel.x = rollDirection.x * rollVelocity;
                vel.y = Mathf.Min(vel.y, 0);
                vel.z = rollDirection.y * rollVelocity;
                rigidbody.linearVelocity = vel;
                rollProgress += deltaTime;
                if (rollProgress >= rollDuration)
                {
                    IsRolling = false;
                }
            }
        }

        private void UpdateLookDirection()
        {
            if (characterLookDirection != LookDirectionOptions.Manual)
            {
                float target;
                if (IsRolling)
                {
                    target = Mathfx.AngleFromVectorDeg(rollDirection);
                    transform.SetYaw(Mathf.MoveTowardsAngle(transform.GetYaw(), target, rotateSpeed * 5 * Time.fixedDeltaTime));
                    return;
                }

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


        public void Roll()
        {
            if (!IsRolling && CanRoll)
            {
                rollProgress = 0;
                IsRolling = true;
                rollDirection = MovementInput.sqrMagnitude > 0.01f ? MovementInput.normalized : transform.forward.GetXZ().normalized;
                lastRollTime = Time.fixedTime;
            }
        }


#if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(transform.position, transform.position + new Vector3(0, suspensionHeight, 0));
        }
#endif
    }
}

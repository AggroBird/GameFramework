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
            Acceleration,
            Input,
            Manual,
        };

        [SerializeField, HideInInspector] private new Rigidbody rigidbody;
        [SerializeField, HideInInspector] private new CapsuleCollider collider;

        [Header("Settings")]
        [SerializeField] private LayerMask _terrainLayer;
        [SerializeField] private LookDirectionOptions _characterLookDirection = LookDirectionOptions.Velocity;
        [SerializeField, Min(0)] private float _rotateSpeed = 500;
        [SerializeField, Min(0.1f)] private float collisionRadius = 0.5f;
        [SerializeField, Min(0.2f)] private float collisionHeight = 1.75f;

        [Header("Height")]
        [SerializeField, Min(0)] private float _rideHeight = 0.25f;
        [SerializeField, Min(0)] private float _rayToGroundLength = 0.5f;
        [SerializeField, Min(0)] public float _rideSpringStrength = 200;
        [SerializeField, Min(0)] private float _rideSpringDamper = 20;

        [Header("Movement")]
        [SerializeField, Min(0)] private float _maxSpeed = 6;
        [SerializeField, Min(0)] private float _acceleration = 400;
        [SerializeField, Min(0)] private float _maxAccelForce = 300;
        [SerializeField] private AnimationCurve _accelerationFactorFromDot;
        [SerializeField] private AnimationCurve _maxAccelerationForceFactorFromDot;

        [Header("Jump")]
        [SerializeField, Min(0)] private float _jumpForceFactor = 16;
        [SerializeField, Min(0)] private float _riseGravityFactor = 3;
        [SerializeField, Min(0)] private float _fallGravityFactor = 8;
        [SerializeField, Min(0)] private float _lowJumpFactor = 10;
        [SerializeField, Min(0)] private float _jumpBuffer = 0.15f;
        [SerializeField, Min(0)] private float _coyoteTime = 0.25f;


        public Vector2 MovementInput { get; set; }
        public bool Jump
        {
            get
            {
                return _jumpInput;
            }
            set
            {
                if (value != _jumpInput)
                {
                    _jumpInput = value;

                    if (value)
                    {
                        _jumpInputTime = Time.fixedTime;
                    }
                }
            }
        }
        private bool _jumpInput = false;

        private Vector3 _gravitationalForce;
        private Vector3 _previousVelocity = Vector3.zero;
        private static PhysicMaterial physicMaterial = default;

        private bool _shouldMaintainHeight = true;

        private float _speedFactor = 1f;
        private float _maxAccelForceFactor = 1f;
        private Vector3 _m_GoalVel = Vector3.zero;

        private float? _jumpInputTime = null;
        private float _timeSinceUngrounded = 0f;
        private float _timeSinceJump = 0f;
        private bool _jumpReady = true;
        private bool _isJumping = false;
        private bool _isGrounded = false;

        public Rigidbody Rigidbody => rigidbody;
        public CapsuleCollider Collider => collider;

        public float ColliderRadius => collisionRadius;
        public float ColliderHeight => collisionHeight;
        public Vector3 Center
        {
            get
            {
                Vector3 position = transform.position;
                position.y += (_rideHeight + collisionHeight) * 0.5f;
                return position;
            }
        }
        public bool IsGrounded => _isGrounded;

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


        protected virtual void Awake()
        {
            rigidbody = GetComponent<Rigidbody>();
            _gravitationalForce = Physics.gravity * rigidbody.mass;

            if (!physicMaterial)
            {
                physicMaterial = new PhysicMaterial();
                physicMaterial.dynamicFriction = physicMaterial.staticFriction = 0;
                physicMaterial.bounciness = 0;
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

                bool rayHitGround = Physics.Raycast(new Ray(transform.position + new Vector3(0, _rideHeight + 0.0001f, 0), Vector3.down), out RaycastHit hit, _rayToGroundLength, _terrainLayer.value);
                _isGrounded = rayHitGround && hit.distance <= _rideHeight * 1.3f;
                if (_isGrounded)
                {
                    _timeSinceUngrounded = 0f;

                    if (_timeSinceJump > 0.2f)
                    {
                        _isJumping = false;
                        _jumpReady = true;
                    }
                }
                else
                {
                    _timeSinceUngrounded += Time.fixedDeltaTime;
                }

                CharacterMove(input);
                CharacterJump(hit);

                if (rayHitGround && _shouldMaintainHeight)
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
            float currHeight = rayHit.distance - _rideHeight;
            float springForce = (currHeight * _rideSpringStrength) - (relVel * _rideSpringDamper);
            Vector3 maintainHeightForce = -_gravitationalForce + springForce * Vector3.down;
            rigidbody.AddForce(maintainHeightForce);
        }

        private void CharacterMove(Vector2 moveInput)
        {
            Vector3 m_UnitGoal = moveInput.Horizontal3D();
            Vector3 unitVel = _m_GoalVel.normalized;
            float velDot = Vector3.Dot(m_UnitGoal, unitVel);
            float accel = _acceleration * _accelerationFactorFromDot.Evaluate(velDot);
            Vector3 goalVel = m_UnitGoal * _maxSpeed * _speedFactor;
            _m_GoalVel = Vector3.MoveTowards(_m_GoalVel, goalVel, accel * Time.fixedDeltaTime);
            Vector3 neededAccel = (_m_GoalVel - rigidbody.velocity) / Time.fixedDeltaTime;
            float maxAccel = _maxAccelForce * _maxAccelerationForceFactorFromDot.Evaluate(velDot) * _maxAccelForceFactor;
            neededAccel = Vector3.ClampMagnitude(neededAccel, maxAccel);
            rigidbody.AddForce(Vector3.Scale(neededAccel * rigidbody.mass, new Vector3(1, 0, 1)));
        }

        private void CharacterJump(RaycastHit rayHit)
        {
            _timeSinceJump += Time.fixedDeltaTime;
            if (rigidbody.velocity.y < 0)
            {
                _shouldMaintainHeight = true;
                if (!_isGrounded)
                {
                    // Increase downforce for a sudden plummet.
                    rigidbody.AddForce(_gravitationalForce * (_fallGravityFactor - 1f)); // Hmm... this feels a bit weird. I want a reactive jump, but I don't want it to dive all the time...
                }
            }
            else if (rigidbody.velocity.y > 0)
            {
                if (!_isGrounded)
                {
                    if (_isJumping)
                    {
                        rigidbody.AddForce(_gravitationalForce * (_riseGravityFactor - 1f));
                    }
                    if (!_jumpInput)
                    {
                        // Impede the jump height to achieve a low jump.
                        rigidbody.AddForce(_gravitationalForce * (_lowJumpFactor - 1f));
                    }
                }
            }

            if (_jumpInputTime.HasValue)
            {
                float timeSinceJumpPressed = Time.fixedTime - _jumpInputTime.Value;
                if (timeSinceJumpPressed < _jumpBuffer && _timeSinceUngrounded < _coyoteTime)
                {
                    if (_jumpReady)
                    {
                        _jumpReady = false;
                        _shouldMaintainHeight = false;
                        _isJumping = true;
                        rigidbody.velocity = new Vector3(rigidbody.velocity.x, 0f, rigidbody.velocity.z); // Cheat fix... (see comment below when adding force to rigidbody).
                        if (rayHit.distance != 0) // i.e. if the ray has hit
                        {
                            rigidbody.position = new Vector3(rigidbody.position.x, rigidbody.position.y - (rayHit.distance - _rideHeight), rigidbody.position.z);
                        }
                        rigidbody.AddForce(Vector3.up * _jumpForceFactor, ForceMode.Impulse); // This does not work very consistently... Jump height is affected by initial y velocity and y position relative to RideHeight... Want to adopt a fancier approach (more like PlayerMovement). A cheat fix to ensure consistency has been issued above...
                        _jumpInputTime = null;
                        _timeSinceJump = 0f;
                    }
                }
            }
        }

        private void UpdateLookDirection()
        {
            if (_characterLookDirection != LookDirectionOptions.Manual)
            {
                float target;
                if (_characterLookDirection == LookDirectionOptions.Velocity || _characterLookDirection == LookDirectionOptions.Acceleration)
                {
                    Vector3 velocity = rigidbody.velocity;
                    velocity.y = 0f;
                    if (_characterLookDirection == LookDirectionOptions.Velocity)
                    {
                        target = Mathfx.AngleFromVectorDeg(velocity.GetXZ());
                    }
                    else
                    {
                        Vector3 deltaVelocity = velocity - _previousVelocity;
                        _previousVelocity = velocity;
                        Vector3 acceleration = deltaVelocity / Time.fixedDeltaTime;
                        target = Mathfx.AngleFromVectorDeg(acceleration.GetXZ());
                    }
                }
                else if (MovementInput.sqrMagnitude > Mathf.Epsilon)
                {
                    target = Mathfx.AngleFromVectorDeg(MovementInput);
                }
                else
                {
                    return;
                }

                transform.SetYaw(Mathf.MoveTowardsAngle(transform.GetYaw(), target, _rotateSpeed * Time.fixedDeltaTime));
            }
        }


#if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(transform.position, transform.position + new Vector3(0, _rideHeight, 0));
        }
        protected virtual void OnValidate()
        {
            if (_rideHeight > _rayToGroundLength) _rayToGroundLength = _rideHeight;

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
                collider.center = new Vector3(0, collisionHeight * 0.5f + _rideHeight, 0);
                collider.direction = 1;
            }
        }
#endif
    }
}

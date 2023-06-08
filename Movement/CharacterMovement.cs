using AggroBird.UnityEngineExtend;
using UnityEngine;

namespace AggroBird.GameFramework
{
    // Based on physics based character controller by Toyful games https://www.toyfulgames.com/
    public class CharacterMovement : Movement
    {
        private enum LookDirectionOptions { Velocity, Acceleration, Input };

        [SerializeField, HideInInspector] private new Rigidbody rigidbody;
        [SerializeField, HideInInspector] private new CapsuleCollider collider;
        private Vector3 _gravitationalForce;
        private Vector3 _previousVelocity = Vector3.zero;
        private static PhysicMaterial physicMaterial = default;

        [Header("Other")]
        [SerializeField] private LayerMask _terrainLayer;
        [SerializeField] private LookDirectionOptions _characterLookDirection = LookDirectionOptions.Velocity;
        [SerializeField] private float _rotateSpeed = 360;

        private bool _shouldMaintainHeight = true;

        [Header("Height Spring")]
        [SerializeField] private float _rideHeight = 2; // rideHeight: desired distance to ground (Note, this is distance from the original raycast position (currently centre of transform)). 
        [SerializeField] private float _rayToGroundLength = 3; // rayToGroundLength: max distance of raycast to ground (Note, this should be greater than the rideHeight).
        [SerializeField] public float _rideSpringStrength = 200; // rideSpringStrength: strength of spring. (?)
        [SerializeField] private float _rideSpringDamper = 10; // rideSpringDampener: dampener of spring. (?)


        private float _speedFactor = 1f;
        private float _maxAccelForceFactor = 1f;
        private Vector3 _m_GoalVel = Vector3.zero;

        [Header("Movement")]
        [SerializeField] private float _maxSpeed = 10;
        [SerializeField] private float _acceleration = 400;
        [SerializeField] private float _maxAccelForce = 300;
        [SerializeField] private AnimationCurve _accelerationFactorFromDot;
        [SerializeField] private AnimationCurve _maxAccelerationForceFactorFromDot;

        private bool jumpInput = false;
        private float _timeSinceJumpPressed = 0f;
        private float _timeSinceUngrounded = 0f;
        private float _timeSinceJump = 0f;
        private bool _jumpReady = true;
        private bool _isJumping = false;

        [Header("Jump")]
        [SerializeField] private bool _canJump = true;
        [SerializeField] private float _jumpForceFactor = 10f;
        [SerializeField] private float _riseGravityFactor = 5f;
        [SerializeField] private float _fallGravityFactor = 10f; // typically > 1f (i.e. 5f).
        [SerializeField] private float _lowJumpFactor = 2.5f;
        [SerializeField] private float _jumpBuffer = 0.15f; // Note, jumpBuffer shouldn't really exceed the time of the jump.
        [SerializeField] private float _coyoteTime = 0.25f;

        [System.NonSerialized] public Vector2 movementInput;



        private void Awake()
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

        /// <summary>
        /// Use the result of a Raycast to determine if the capsules distance from the ground is sufficiently close to the desired ride height such that the character can be considered 'grounded'.
        /// </summary>
        /// <param name="rayHitGround">Whether or not the Raycast hit anything.</param>
        /// <param name="rayHit">Information about the ray.</param>
        /// <returns>Whether or not the player is considered grounded.</returns>
        private bool CheckIfGrounded(bool rayHitGround, RaycastHit rayHit)
        {
            bool grounded;
            if (rayHitGround == true)
            {
                grounded = rayHit.distance <= _rideHeight * 1.3f; // 1.3f allows for greater leniancy (as the value will oscillate about the rideHeight).
            }
            else
            {
                grounded = false;
            }
            return grounded;
        }

        private void UpdateLookDirection()
        {
            float target;
            if (_characterLookDirection == LookDirectionOptions.Velocity || _characterLookDirection == LookDirectionOptions.Acceleration)
            {
                Vector3 velocity = rigidbody.velocity;
                velocity.y = 0f;
                if (_characterLookDirection == LookDirectionOptions.Velocity)
                {
                    target = Mathfx.AngleFromVectorDeg(velocity);
                }
                else
                {
                    Vector3 deltaVelocity = velocity - _previousVelocity;
                    _previousVelocity = velocity;
                    Vector3 acceleration = deltaVelocity / Time.fixedDeltaTime;
                    target = Mathfx.AngleFromVectorDeg(acceleration);
                }
            }
            else if (movementInput.sqrMagnitude > 0)
            {
                target = Mathfx.AngleFromVectorDeg(movementInput);
            }
            else
            {
                return;
            }

            transform.SetYaw(Mathf.MoveTowardsAngle(transform.GetYaw(), target, _rotateSpeed * Time.fixedDeltaTime));
        }

        /// <summary>
        /// Determines and plays the appropriate character sounds, particle effects, then calls the appropriate methods to move and float the character.
        /// </summary>
        private void FixedUpdate()
        {
            // Get normalized input
            Vector2 input = movementInput;
            float len = input.magnitude;
            if (len > 1)
            {
                input /= len;
            }

            bool rayHitGround = RaycastToGround(out RaycastHit hit);
            bool grounded = CheckIfGrounded(rayHitGround, hit);
            if (grounded == true)
            {
                _timeSinceUngrounded = 0f;

                if (_timeSinceJump > 0.2f)
                {
                    _isJumping = false;
                }
            }
            else
            {
                _timeSinceUngrounded += Time.fixedDeltaTime;
            }

            CharacterMove(input);
            CharacterJump(jumpInput, grounded, hit);

            if (rayHitGround && _shouldMaintainHeight)
            {
                MaintainHeight(hit);
            }

            UpdateLookDirection();
        }

        /// <summary>
        /// Perfom raycast towards the ground.
        /// </summary>
        /// <returns>Whether the ray hit the ground, and information about the ray.</returns>
        private bool RaycastToGround(out RaycastHit hit)
        {
            return Physics.Raycast(new Ray(transform.position, Vector3.down), out hit, _rayToGroundLength, _terrainLayer.value);
        }

        /// <summary>
        /// Determines the relative velocity of the character to the ground beneath,
        /// Calculates and applies the oscillator force to bring the character towards the desired ride height.
        /// Additionally applies the oscillator force to the squash and stretch oscillator, and any object beneath.
        /// </summary>
        /// <param name="rayHit">Information about the RaycastToGround.</param>
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

        public void Jump()
        {
            if (_canJump)
            {
                jumpInput = true;
                _timeSinceJumpPressed = 0f;
            }
        }

        /// <summary>
        /// Apply forces to move the character up to a maximum acceleration, with consideration to acceleration graphs.
        /// </summary>
        /// <param name="moveInput">The player movement input.</param>
        /// <param name="rayHit">The rayHit towards the platform.</param>
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
            rigidbody.AddForceAtPosition(Vector3.Scale(neededAccel * rigidbody.mass, new Vector3(1, 0, 1)), transform.position);
        }

        /// <summary>
        /// Apply force to cause the character to perform a single jump, including coyote time and a jump input buffer.
        /// </summary>
        /// <param name="jumpInput">The player jump input.</param>
        /// <param name="grounded">Whether or not the player is considered grounded.</param>
        /// <param name="rayHit">The rayHit towards the platform.</param>
        private void CharacterJump(bool jumpInput, bool grounded, RaycastHit rayHit)
        {
            _timeSinceJumpPressed += Time.fixedDeltaTime;
            _timeSinceJump += Time.fixedDeltaTime;
            if (rigidbody.velocity.y < 0)
            {
                _shouldMaintainHeight = true;
                _jumpReady = true;
                if (!grounded)
                {
                    // Increase downforce for a sudden plummet.
                    rigidbody.AddForce(_gravitationalForce * (_fallGravityFactor - 1f)); // Hmm... this feels a bit weird. I want a reactive jump, but I don't want it to dive all the time...
                }
            }
            else if (rigidbody.velocity.y > 0)
            {
                if (!grounded)
                {
                    if (_isJumping)
                    {
                        rigidbody.AddForce(_gravitationalForce * (_riseGravityFactor - 1f));
                    }
                    if (!jumpInput)
                    {
                        // Impede the jump height to achieve a low jump.
                        rigidbody.AddForce(_gravitationalForce * (_lowJumpFactor - 1f));
                    }
                }
            }

            if (_timeSinceJumpPressed < _jumpBuffer && _timeSinceUngrounded < _coyoteTime)
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
                    _timeSinceJumpPressed = _jumpBuffer; // So as to not activate further jumps, in the case that the player lands before the jump timer surpasses the buffer.
                    _timeSinceJump = 0f;
                }
            }
        }

#if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(transform.position, transform.position - new Vector3(0, _rideHeight, 0));
        }
        protected virtual void OnValidate()
        {
            if (_rideHeight > _rayToGroundLength) _rayToGroundLength = _rideHeight;

            if (Utility.EnsureComponentReference(this, ref rigidbody))
            {

            }
            Utility.EnsureComponentReference(this, ref collider);
        }
#endif
    }
}

using AggroBird.UnityEngineExtend;
using UnityEngine;

namespace AggroBird.GameFramework
{
    [DefaultExecutionOrder(99999)]
    public class FollowCamera : MonoBehaviour, IPlayerCamera
    {
        [SerializeField] private Camera cameraComponent;
        public Camera Camera => cameraComponent;

        [Clamped(min: 0)] public int playerIndex = 0;
        [Space]
        public LayerMask collisionMask;
        [Space]
        [Clamped(min: 0)] public float linearFollowSpeed = 10;
        [Clamped(min: 0)] public float angularFollowSpeed = 5;
        [Space]
        [Clamped(0, 90)]
        public float pitch = 10;
        public Vector3 originOffset = new Vector3(0, 2, 0);
        public Vector3 followOffset = new Vector3(0, 3, -5);
        [Space]
        [Clamped(min: 0)] public float collisionRadius = 0.35f;

        public float minPitch = -30;
        public float maxPitch = 60;

        public virtual float FieldOfView
        {
            get => cameraComponent.fieldOfView;
            set => cameraComponent.fieldOfView = value;
        }


        [System.NonSerialized]
        public Rotator2 currentRotation;

        protected Pawn CurrentTarget { get; private set; }

        private Vector3 currentPosition;
        private float inputForce;
        private float offsetLength;
        private Vector3 targetPreviousPosition;

        private bool isOverride;
        private Vector3 overrideOriginPosition;
        private Vector3 overrideTargetPosition;
        private Quaternion overrideOriginRotation;
        private Quaternion overrideTargetRotation;
        private float overrideDuration;
        private float overrideStartTime;

        protected bool TryGetPlayer<T>(out T player) where T : Player
        {
            if (AppInstance.TryGetInstance(out AppInstance instance) && instance.TryGetPlayer(playerIndex, out player))
            {
                return true;
            }
            player = null;
            return false;
        }



        protected virtual void Awake()
        {
            offsetLength = followOffset.magnitude;

            currentRotation = new Rotator2(pitch, transform.eulerAngles.y);
            currentPosition = transform.position - Quaternion.Euler(currentRotation.pitch, currentRotation.yaw, 0) * followOffset;
        }

        protected virtual void LateUpdate()
        {
            Pawn target = null;
            if (TryGetPlayer(out Player player))
            {
                target = player.Pawn;
            }

            if (!ReferenceEquals(CurrentTarget, target))
            {
                bool hadTarget = !ReferenceEquals(CurrentTarget, null);
                if (CurrentTarget && CurrentTarget.TryGetOwner(out Player currentOwner))
                {
                    currentOwner.UnregisterCamera(this);
                }

                CurrentTarget = target;

                if (CurrentTarget)
                {
                    if (CurrentTarget.TryGetOwner(out currentOwner))
                    {
                        currentOwner.RegisterCamera(this);
                    }

                    Vector3 currentTargetPosition = CurrentTarget.transform.position + originOffset;
                    if (!hadTarget || Vector3.Distance(currentTargetPosition, currentPosition) > 5)
                    {
                        currentRotation.yaw = CurrentTarget.transform.eulerAngles.y;
                        currentPosition = targetPreviousPosition = currentTargetPosition;
                        transform.eulerAngles = new Vector3(pitch + currentRotation.pitch, currentRotation.yaw, 0);
                    }
                }
            }

            if (!isOverride && CurrentTarget && CurrentTarget.TryGetOwner(out Player owner))
            {
                if (owner.TryGetController(out Controller controller))
                {
                    Vector2 cameraInput = controller.CameraInput;
                    inputForce += cameraInput.magnitude;
                    if (inputForce > 1.5f) inputForce = 1.5f;
                    currentRotation.pitch = Mathf.Clamp(currentRotation.pitch - cameraInput.y, minPitch, maxPitch);
                    currentRotation.yaw += cameraInput.x;
                    currentRotation.yaw = Mathfx.ModAbs(currentRotation.yaw, 360);
                }
            }

            inputForce -= Time.deltaTime;
            if (inputForce < 0) inputForce = 0;
        }

        protected virtual void FixedUpdate()
        {
            if (CurrentTarget)
            {
                float deltaTime = Time.deltaTime;

                // Calculate current target velocity
                Vector3 targetPosition = CurrentTarget.Center + originOffset;
                Vector2 velocity = (targetPreviousPosition.GetXZ() - targetPosition.GetXZ()) / deltaTime;
                targetPreviousPosition = targetPosition;

                // Update rotation
                if (CurrentTarget.rotateCamera)
                {
                    Rotator3 targetRot = Rotator3.FromEuler(CurrentTarget.transform.eulerAngles);
                    float rotateSpeed = (1 - Mathf.Clamp01(inputForce)) * Mathf.Clamp01((velocity.magnitude - 0.1f) / 3);
                    // Only rotate if the player is moving and we havent changed the camera recently
                    if (rotateSpeed > 0)
                    {
                        rotateSpeed *= angularFollowSpeed * deltaTime;
                        float pitchRotation = Mathf.Abs(Mathf.DeltaAngle(targetRot.pitch, currentRotation.pitch)) * rotateSpeed;
                        currentRotation.pitch = Mathf.MoveTowardsAngle(currentRotation.pitch, 0, pitchRotation);
                        float yawRotation = Mathf.Abs(Mathf.DeltaAngle(targetRot.yaw, currentRotation.yaw)) * rotateSpeed;
                        currentRotation.yaw = Mathf.MoveTowardsAngle(currentRotation.yaw, targetRot.yaw, yawRotation);
                    }
                }
                Quaternion setRotation = Quaternion.Euler(pitch + currentRotation.pitch, currentRotation.yaw, 0);

                // Update position
                {
                    float dist = Vector3.Distance(currentPosition, targetPosition) * linearFollowSpeed * deltaTime;
                    currentPosition = Vector3.MoveTowards(currentPosition, targetPosition, dist);
                }

                // Raycast for collisions
                Vector3 setPosition = currentPosition;
                {
                    Vector3 cameraPosition = currentPosition + Quaternion.Euler(currentRotation.pitch, currentRotation.yaw, 0) * followOffset;
                    Vector3 direction = cameraPosition - targetPosition;
                    float length = direction.magnitude;
                    if (length < Mathf.Epsilon)
                    {
                        setPosition = targetPosition;
                    }
                    else
                    {
                        Vector3 normal = direction / length;
                        if (Physics.SphereCast(targetPosition, collisionRadius, normal, out RaycastHit hit, length, collisionMask))
                        {
                            offsetLength = hit.distance;
                        }
                        else
                        {
                            offsetLength = Mathf.MoveTowards(offsetLength, length, length * 3 * deltaTime);
                        }
                        setPosition = targetPosition + normal * offsetLength;
                    }
                }

                if (isOverride)
                {
                    float t = Mathf.Clamp01((Time.time - overrideStartTime) / overrideDuration);
                    if (t >= 1)
                    {
                        setPosition = overrideTargetPosition;
                        setRotation = overrideTargetRotation;
                    }
                    else
                    {
                        t = Mathfx.InvPow(t, 2);
                        setPosition = Vector3.Lerp(overrideOriginPosition, overrideTargetPosition, t);
                        setRotation = Quaternion.Slerp(overrideOriginRotation, overrideTargetRotation, t);
                    }
                }

                transform.SetPositionAndRotation(setPosition, setRotation);
            }
        }

        public void Override(Vector3 position, Quaternion rotation, float duration)
        {
            isOverride = true;
            overrideOriginPosition = transform.position;
            overrideTargetPosition = position;
            overrideOriginRotation = transform.rotation;
            overrideTargetRotation = rotation;
            overrideDuration = Mathf.Max(0.001f, duration);
            overrideStartTime = Time.time;
        }
        public void ClearOverride()
        {
            isOverride = false;
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (minPitch > maxPitch) minPitch = maxPitch;
        }
#endif
    }
}

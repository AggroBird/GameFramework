using AggroBird.UnityExtend;
using UnityEngine;

namespace AggroBird.GameFramework
{
    public enum AutoFollowRotationMode
    {
        None = 0,
        Pitch = 1,
        Yaw = 2,
        BothPitchAndYaw = 3,
    }

    [RequireComponent(typeof(Camera))]
    public class FollowCamera : PlayerCamera
    {
        [SerializeField, HideInInspector] private Camera cameraComponent;
        public override Camera Camera => cameraComponent;

        [Space]
        public LayerMask collisionMask = 1;
        [Space]
        [Clamped(min: 0)] public float collisionRadius = 0.35f;

        private Vector3 followPosition;
        private Quaternion followRotation;

        // Position/rotation that the follow camera desires
        public override Vector3 Position =>
#if UNITY_EDITOR
            !Application.IsPlaying(gameObject) ? base.Position :
#endif
            followPosition;
        public override Quaternion Rotation =>
#if UNITY_EDITOR
            !Application.IsPlaying(gameObject) ? base.Rotation :
# endif
            followRotation;

        [System.NonSerialized]
        public Rotator2 rotation;
        [System.NonSerialized]
        public bool updateInput = true;
        [System.NonSerialized]
        public bool updatePosition = true;

        protected Pawn CurrentTarget { get; private set; }
        protected Vector3 CurrentTargetPosition => CurrentTarget.Center + CurrentTarget.originOffset;

        public AutoFollowRotationMode autoFollowRotationMode = AutoFollowRotationMode.BothPitchAndYaw;
        public QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.Ignore;

        private Vector3 targetCurrentPosition;
        private float inputForce;
        private float offsetLength;
        private Vector3 targetPreviousPosition;

        private enum OverrideState
        {
            None = 0,
            In,
            Out,
        }
        private OverrideState overrideState = OverrideState.None;
        private Vector3 overrideOriginPosition;
        private Vector3 overrideTargetPosition;
        private Quaternion overrideOriginRotation;
        private Quaternion overrideTargetRotation;
        private float overrideOriginFov;
        private float overrideTargetFov;
        private float overrideDuration;
        private float overrideStartTime;


        protected virtual void Awake()
        {
#if UNITY_EDITOR
            if (!cameraComponent)
            {
                cameraComponent = GetComponent<Camera>();
            }
#endif

            /*offsetLength = followOffset.magnitude;

            rotation = new Rotator2(pitch, transform.eulerAngles.y);
            targetCurrentPosition = transform.position - Quaternion.Euler(rotation.pitch, rotation.yaw, 0) * followOffset;

            followPosition = transform.position;
            followRotation = transform.rotation;*/
        }

        protected override void UpdateInput()
        {
            base.UpdateInput();

            if (Owner)
            {
                Pawn target = Owner.Pawn;
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
                        var currentTarget = CurrentTarget;
                        Vector3 targetPosition = CurrentTargetPosition;
                        if (!hadTarget || Vector3.Distance(targetPosition, targetCurrentPosition) > 5)
                        {
                            rotation.yaw = CurrentTarget.transform.eulerAngles.y;
                            targetCurrentPosition = targetPreviousPosition = targetPosition;
                            transform.rotation = Quaternion.Euler(currentTarget.pitch + rotation.pitch, rotation.yaw, 0);
                        }
                    }
                }

                if (updateInput && overrideState != OverrideState.In && CurrentTarget && CurrentTarget.allowCameraRotation && CurrentTarget.TryGetOwner(out Player owner))
                {
                    if (owner.InputEnabled && owner.TryGetController(out Controller controller))
                    {
                        Vector2 cameraInput = controller.CameraInput.GetValue();
                        inputForce += cameraInput.magnitude;
                        if (inputForce > 1.5f) inputForce = 1.5f;
                        rotation.pitch = CurrentTarget.pitchRange.Clamp(rotation.pitch - cameraInput.y);
                        rotation.yaw += cameraInput.x;
                        rotation.yaw = Mathfx.ModAbs(rotation.yaw, 360);
                    }
                }

                inputForce -= Time.deltaTime;
                if (inputForce < 0) inputForce = 0;
            }
        }

        protected override void UpdateTransform()
        {
            if (Application.IsPlaying(gameObject) && CurrentTarget && updatePosition)
            {
                float deltaTime = Time.deltaTime;
                var currentTarget = CurrentTarget;

                // Calculate current target velocity
                Vector3 targetPosition = CurrentTargetPosition;
                Vector2 velocity = deltaTime > Mathf.Epsilon ? (targetPreviousPosition.GetXZ() - targetPosition.GetXZ()) / deltaTime : Vector2.zero;
                targetPreviousPosition = targetPosition;

                // Update rotation
                AutoFollowRotationMode followMode = currentTarget.cameraAutoFollowRotationMode & autoFollowRotationMode;
                if (followMode != AutoFollowRotationMode.None)
                {
                    Rotator3 targetRot = Rotator3.FromEuler(currentTarget.transform.eulerAngles);
                    float rotateSpeed = (1 - Mathf.Clamp01(inputForce)) * Mathf.Clamp01((velocity.magnitude - 0.1f) / 3);
                    // Only rotate if the player is moving and we havent changed the camera recently
                    if (rotateSpeed > 0)
                    {
                        if ((followMode & AutoFollowRotationMode.Pitch) != AutoFollowRotationMode.None)
                        {
                            float pitchRotation = Mathf.Abs(Mathf.DeltaAngle(targetRot.pitch, rotation.pitch)) * rotateSpeed * currentTarget.angularFollowSpeed.pitch * deltaTime;
                            rotation.pitch = Mathf.MoveTowardsAngle(rotation.pitch, 0, pitchRotation);
                        }
                        if ((followMode & AutoFollowRotationMode.Yaw) != AutoFollowRotationMode.None)
                        {
                            float yawRotation = Mathf.Abs(Mathf.DeltaAngle(targetRot.yaw, rotation.yaw)) * rotateSpeed * currentTarget.angularFollowSpeed.yaw * deltaTime;
                            rotation.yaw = Mathf.MoveTowardsAngle(rotation.yaw, targetRot.yaw, yawRotation);
                        }
                    }
                }
                Quaternion setRotation = Quaternion.Euler(currentTarget.pitch + rotation.pitch, rotation.yaw, 0);

                // Update position
                {
                    static Vector2 FollowLinearHorizontal(Vector2 current, Vector2 target, float speed)
                    {
                        return Vector2.MoveTowards(current, target, Vector2.Distance(current, target) * speed);
                    }
                    static float FollowLinearVertical(float current, float target, float speed)
                    {
                        return Mathf.MoveTowards(current, target, Mathf.Abs(current - target) * speed);
                    }

                    targetCurrentPosition.SetXZ(FollowLinearHorizontal(targetCurrentPosition.GetXZ(), targetPosition.GetXZ(), currentTarget.linearHorizontalFollowSpeed * deltaTime));
                    targetCurrentPosition.y = FollowLinearVertical(targetCurrentPosition.y, targetPosition.y, currentTarget.linearVerticalFollowSpeed * deltaTime);
                }

                // Raycast for collisions
                Vector3 setPosition;
                {
                    Vector3 cameraPosition = targetCurrentPosition + Quaternion.Euler(rotation.pitch, rotation.yaw, 0) * currentTarget.followOffset;
                    Vector3 direction = cameraPosition - targetPosition;
                    float length = direction.magnitude;
                    if (length < Mathf.Epsilon)
                    {
                        setPosition = targetPosition;
                    }
                    else
                    {
                        Vector3 normal = direction / length;
                        if (Physics.SphereCast(targetPosition, collisionRadius, normal, out RaycastHit hit, length, collisionMask, queryTriggerInteraction))
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

                if (overrideState != OverrideState.None)
                {
                    float t = Mathfx.InvPow(Mathf.Clamp01((Time.time - overrideStartTime) / overrideDuration), 2);
                    bool finished = t >= 0.999f;
                    if (finished) t = 1;
                    switch (overrideState)
                    {
                        case OverrideState.In:
                            setPosition = Vector3.Lerp(overrideOriginPosition, overrideTargetPosition, t);
                            setRotation = Quaternion.Slerp(overrideOriginRotation, overrideTargetRotation, t);
                            FieldOfView = Mathf.Lerp(overrideOriginFov, overrideTargetFov, t);
                            break;
                        case OverrideState.Out:
                            setPosition = Vector3.Lerp(overrideOriginPosition, setPosition, t);
                            setRotation = Quaternion.Slerp(overrideOriginRotation, setRotation, t);
                            FieldOfView = Mathf.Lerp(overrideTargetFov, overrideOriginFov, t);
                            if (finished) overrideState = OverrideState.None;
                            break;
                    }
                }

                followPosition = setPosition;
                followRotation = setRotation;
                transform.SetPositionAndRotation(followPosition, followRotation);
            }
        }

        public void SetOverride(Vector3 position, Quaternion rotation, float duration = 1)
        {
            SetOverride(position, rotation, FieldOfView, duration);
        }
        public void SetOverride(Vector3 position, Quaternion rotation, float fieldOfView, float duration = 1)
        {
            overrideState = OverrideState.In;
            overrideOriginPosition = transform.position;
            overrideTargetPosition = position;
            overrideOriginRotation = transform.rotation;
            overrideTargetRotation = rotation;
            overrideOriginFov = FieldOfView;
            overrideTargetFov = fieldOfView;
            overrideDuration = Mathf.Max(0.001f, duration);
            overrideStartTime = Time.time;
        }
        public void UpdateOverride(Vector3 position, Quaternion rotation)
        {
            UpdateOverride(position, rotation, overrideTargetFov);
        }
        public void UpdateOverride(Vector3 position, Quaternion rotation, float fieldOfView)
        {
            overrideTargetPosition = position;
            overrideTargetRotation = rotation;
            overrideTargetFov = fieldOfView;
        }
        public void ClearOverride(float duration = 1)
        {
            if (overrideState == OverrideState.In)
            {
                overrideState = OverrideState.Out;
                overrideOriginPosition = transform.position;
                overrideOriginRotation = transform.rotation;
                overrideDuration = Mathf.Max(0.001f, duration);
                overrideStartTime = Time.time;
            }
        }

        protected virtual void OnValidate()
        {
            Utility.EnsureComponentReference(this, ref cameraComponent);
        }
    }
}

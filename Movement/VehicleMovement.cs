using UnityEngine;

namespace AggroBird.GameFramework
{
    public class VehicleMovement : MonoBehaviour
    {
        [SerializeField] private new Rigidbody rigidbody;
        [SerializeField] private Transform[] wheels;
        [SerializeField] private Transform[] meshes;
        [SerializeField] private float topSpeed = 10;
        [Space]
        [SerializeField] private float suspensionHeight = 0.5f;
        [SerializeField] private float springStrength = 30;
        [SerializeField] private float springDamp = 5;
        [Space]
        [SerializeField] private float maxSteerAngle = 30;
        [SerializeField] private float steerSpeed = 3;
        [Space]
        [SerializeField] private float tireMass = 10;
        [Space]
        [SerializeField] private float torqueScale = 500;
        [SerializeField] private AnimationCurve torqueCurve;
        [Space]
        [SerializeField] private float frictionScale = 5;
        [SerializeField] private AnimationCurve frontWheelFrictionCurve;
        [SerializeField] private AnimationCurve backWheelFrictionCurve;

        public int Throttle { get; set; }
        public int Steer { get; set; }

        private float steerValue = 0;


        private void Update()
        {
            float vehicleSpeed = Vector3.Dot(transform.forward, rigidbody.velocity);
            float normalizedSpeed = Mathf.Clamp01(Mathf.Abs(vehicleSpeed) / topSpeed);
            DrawCurve(torqueCurve, new Rect(0, 0, 200, 100), normalizedSpeed);

            for (int i = 0; i < 4; i++)
            {
                var wheel = wheels[i];
                bool isFrontWheel = i < 2;
                Vector3 tireWorldVel = rigidbody.GetPointVelocity(wheel.position);
                float x = Vector3.Dot(wheel.right, tireWorldVel);
                var curve = isFrontWheel ? frontWheelFrictionCurve : backWheelFrictionCurve;
                DrawCurve(curve, new Rect(0, 100 * i + 100, 200, 100), Mathf.Clamp01(Mathf.Abs(x) / frictionScale));
            }
        }

        private void FixedUpdate()
        {
            Throttle = Mathf.Clamp(Throttle, -1, 1);
            steerValue = Mathf.MoveTowards(steerValue, Steer, steerSpeed * Time.deltaTime);

            float vehicleSpeed = Vector3.Dot(transform.forward, rigidbody.velocity);
            float normalizedSpeed = Mathf.Clamp01(Mathf.Abs(vehicleSpeed) / topSpeed);
            float torque = normalizedSpeed > 1 ? 0 : torqueCurve.Evaluate(normalizedSpeed) * torqueScale;

            Vector3[] addVelocities = new Vector3[4];
            for (int i = 0; i < wheels.Length; i++)
            {
                var wheel = wheels[i];
                bool isFrontWheel = i < 2;

                if (isFrontWheel)
                {
                    wheel.transform.localEulerAngles = new Vector3(0, maxSteerAngle * steerValue, 0);
                }

                if (Physics.Raycast(wheel.position, -wheel.up, out RaycastHit hit, 1, 1))
                {
                    meshes[i].transform.localPosition = new Vector3(0, suspensionHeight - hit.distance, 0);

                    Vector3 tireWorldVel = rigidbody.GetPointVelocity(wheel.position);

                    // Suspension
                    float offset = suspensionHeight - hit.distance;
                    float y = Vector3.Dot(wheel.up, tireWorldVel);
                    float force = offset * springStrength - y * springDamp;
                    addVelocities[i] += wheel.up * force;

                    // Friction
                    float x = Vector3.Dot(wheel.right, tireWorldVel);
                    float slideFriction = CalculateFriction(x, isFrontWheel);
                    addVelocities[i] += wheel.right * CalculateFrictionForce(x, slideFriction);

                    if (isFrontWheel)
                    {
                        if (Throttle > 0 || (Throttle < 0 && vehicleSpeed <= 0.1f))
                        {
                            addVelocities[i] += wheel.forward * Throttle * torque * slideFriction;
                        }
                    }
                    else
                    {
                        int sign = (int)Mathf.Sign(vehicleSpeed);
                        if (Throttle != 0 && sign != 0 && sign != Throttle)
                        {
                            float z = Vector3.Dot(wheel.forward, tireWorldVel);
                            float brakeFriction = CalculateFriction(z, isFrontWheel);
                            addVelocities[i] += wheel.forward * CalculateFrictionForce(z, brakeFriction);
                        }
                    }
                }
                else
                {
                    meshes[i].transform.localPosition = Vector3.zero;
                }
            }
            for (int i = 0; i < wheels.Length; i++)
            {
                rigidbody.AddForceAtPosition(addVelocities[i], wheels[i].position);
            }
        }

        private float CalculateFriction(float velocity, bool isFrontWheel)
        {
            float frictionValue = Mathf.Clamp01(Mathf.Abs(velocity) / frictionScale);
            return isFrontWheel ? frontWheelFrictionCurve.Evaluate(frictionValue) : backWheelFrictionCurve.Evaluate(frictionValue);
        }
        private float CalculateFrictionForce(float velocity, float friction)
        {
            float desiredVelChange = -velocity * friction;
            float desiredAccel = desiredVelChange / Time.deltaTime;
            return tireMass * desiredAccel;
        }


        private void DrawCurve(AnimationCurve curve, Rect position, params float[] values)
        {
            Camera camera = Camera.main;
            if (camera)
            {
                Vector3 right = camera.transform.right;
                Vector3 up = camera.transform.up;
                Vector3 p0 = camera.ScreenToWorldPoint(new Vector3(0, 0, 1));
                Vector3 p1 = camera.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, 1));
                Vector3 dif = p1 - p0;
                float w = Vector3.Dot(camera.transform.right, dif);
                float h = Vector3.Dot(camera.transform.up, dif);
                Vector3 ScreenToWorld(float x, float y)
                {
                    x += position.x;
                    y += Screen.height - (position.y + position.height);
                    x /= Screen.width;
                    y /= Screen.height;
                    return p0 + right * w * x + up * h * y;
                }
                Debug.DrawLine(ScreenToWorld(0, position.height), ScreenToWorld(position.width, position.height), Color.white);
                Debug.DrawLine(ScreenToWorld(0, 0), ScreenToWorld(position.width, 0), Color.white);
                int IterCount = (int)position.width / 4;
                float Step = 1.0f / IterCount;
                float f = Step;
                float y = curve.Evaluate(0);
                for (int i = 0; i < IterCount; i++, f += Step)
                {
                    float next = curve.Evaluate(f);
                    Debug.DrawLine(ScreenToWorld(i * 4, y * (position.height - 2) + 1), ScreenToWorld((i + 1) * 4, next * (position.height - 2) + 1), Color.green);
                    y = next;
                }
                if (values != null)
                {
                    for (int i = 0; i < values.Length; i++)
                    {
                        float value = values[i];
                        float x = position.width * value;
                        y = curve.Evaluate(value);
                        Debug.DrawLine(ScreenToWorld(x, 1), ScreenToWorld(x, y * (position.height - 2) + 1), Color.yellow);
                    }
                }
            }
        }
    }
}
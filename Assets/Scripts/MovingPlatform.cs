using UnityEngine;

namespace Mindrift.World
{
    public sealed class MovingPlatform : MonoBehaviour
    {
        public enum MotionMode
        {
            AxisAmplitude = 0,
            ExplicitEndpoints = 1
        }

        [Header("Motion")]
        [SerializeField] private MotionMode motionMode = MotionMode.AxisAmplitude;
        [SerializeField] private float cycleDuration = 3f;
        [SerializeField] private float phaseOffset;
        [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Axis/Amplitude")]
        [SerializeField] private Vector3 localAxis = Vector3.right;
        [SerializeField] private float amplitude = 2f;

        [Header("Endpoints")]
        [SerializeField] private Vector3 localStartOffset = new Vector3(-2f, 0f, 0f);
        [SerializeField] private Vector3 localEndOffset = new Vector3(2f, 0f, 0f);

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        private Transform cachedTransform;
        private Vector3 initialLocalPosition;

        public Vector3 FrameDelta { get; private set; }

        private void Awake()
        {
            cachedTransform = transform;
            initialLocalPosition = cachedTransform.localPosition;
        }

        private void LateUpdate()
        {
            if (cycleDuration <= 0.001f)
            {
                FrameDelta = Vector3.zero;
                return;
            }

            float pingPong = Mathf.PingPong((Time.time + phaseOffset) / cycleDuration, 1f);
            float t = movementCurve.Evaluate(pingPong);

            Vector3 targetLocalPosition;
            if (motionMode == MotionMode.AxisAmplitude)
            {
                Vector3 axis = localAxis.sqrMagnitude > 0f ? localAxis.normalized : Vector3.right;
                targetLocalPosition = initialLocalPosition + axis * Mathf.Lerp(-amplitude, amplitude, t);
            }
            else
            {
                targetLocalPosition = initialLocalPosition + Vector3.Lerp(localStartOffset, localEndOffset, t);
            }

            Vector3 oldPosition = cachedTransform.position;
            if (cachedTransform.parent != null)
            {
                cachedTransform.position = cachedTransform.parent.TransformPoint(targetLocalPosition);
            }
            else
            {
                cachedTransform.position = targetLocalPosition;
            }

            FrameDelta = cachedTransform.position - oldPosition;
        }

        private void OnDisable()
        {
            FrameDelta = Vector3.zero;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos)
            {
                return;
            }

            Gizmos.color = new Color(0f, 1f, 1f, 0.8f);
            Vector3 origin = Application.isPlaying ? initialLocalPosition : transform.localPosition;

            if (motionMode == MotionMode.AxisAmplitude)
            {
                Vector3 axis = localAxis.sqrMagnitude > 0f ? localAxis.normalized : Vector3.right;
                Vector3 a = transform.parent != null
                    ? transform.parent.TransformPoint(origin + axis * -amplitude)
                    : origin + axis * -amplitude;
                Vector3 b = transform.parent != null
                    ? transform.parent.TransformPoint(origin + axis * amplitude)
                    : origin + axis * amplitude;
                Gizmos.DrawLine(a, b);
                Gizmos.DrawWireSphere(a, 0.2f);
                Gizmos.DrawWireSphere(b, 0.2f);
            }
            else
            {
                Vector3 a = transform.parent != null
                    ? transform.parent.TransformPoint(origin + localStartOffset)
                    : origin + localStartOffset;
                Vector3 b = transform.parent != null
                    ? transform.parent.TransformPoint(origin + localEndOffset)
                    : origin + localEndOffset;
                Gizmos.DrawLine(a, b);
                Gizmos.DrawWireCube(a, Vector3.one * 0.2f);
                Gizmos.DrawWireCube(b, Vector3.one * 0.2f);
            }
        }
    }
}

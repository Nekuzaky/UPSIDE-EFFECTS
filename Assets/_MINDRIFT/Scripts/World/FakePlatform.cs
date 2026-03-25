using UnityEngine;

namespace Mindrift.World
{
    public sealed class FakePlatform : MonoBehaviour
    {
        public enum FakeBehaviorMode
        {
            IntensitySolidity = 0,
            Flicker = 1,
            IntensityAndFlicker = 2
        }

        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int EmissiveColorProperty = Shader.PropertyToID("_EmissiveColor");

        [Header("References")]
        [SerializeField] private Collider platformCollider;
        [SerializeField] private Renderer[] renderers;

        [Header("Behavior")]
        [SerializeField] private FakeBehaviorMode behaviorMode = FakeBehaviorMode.IntensityAndFlicker;
        [SerializeField, Range(0f, 1f)] private float intensityUnsafeThreshold = 0.72f;
        [SerializeField, Range(0f, 1f)] private float flickerStartThreshold = 0.45f;

        [Header("Flicker")]
        [SerializeField] private Vector2 safeDurationRange = new Vector2(0.7f, 1.6f);
        [SerializeField] private Vector2 unsafeDurationRange = new Vector2(0.12f, 0.5f);
        [SerializeField, Range(0f, 1f)] private float unsafeChance = 0.45f;

        [Header("Visual")]
        [SerializeField] private bool remainVisibleWhenUnsafe = true;
        [SerializeField] private Color unsafeTint = new Color(1f, 0.2f, 0.6f, 1f);
        [SerializeField, Range(0f, 1f)] private float unsafeEmissionScale = 0.15f;

        private float globalIntensity;
        private float forcedUnsafeTimer;
        private float flickerTimer;
        private bool flickerUnsafe;
        private bool isSolid = true;
        private MaterialPropertyBlock block;

        private void Awake()
        {
            if (platformCollider == null)
            {
                platformCollider = GetComponent<Collider>();
            }

            if ((renderers == null || renderers.Length == 0))
            {
                renderers = GetComponentsInChildren<Renderer>();
            }

            block = new MaterialPropertyBlock();
            ScheduleNextFlicker();
            ApplyState(true);
        }

        private void Update()
        {
            if (forcedUnsafeTimer > 0f)
            {
                forcedUnsafeTimer -= Time.deltaTime;
            }

            bool shouldFlicker = behaviorMode == FakeBehaviorMode.Flicker || behaviorMode == FakeBehaviorMode.IntensityAndFlicker;
            if (shouldFlicker && globalIntensity >= flickerStartThreshold)
            {
                flickerTimer -= Time.deltaTime;
                if (flickerTimer <= 0f)
                {
                    flickerUnsafe = Random.value < unsafeChance;
                    ScheduleNextFlicker();
                }
            }
            else
            {
                flickerUnsafe = false;
            }

            bool unsafeByIntensity = behaviorMode == FakeBehaviorMode.IntensitySolidity || behaviorMode == FakeBehaviorMode.IntensityAndFlicker;
            unsafeByIntensity &= globalIntensity >= intensityUnsafeThreshold;

            bool shouldBeUnsafe = forcedUnsafeTimer > 0f || unsafeByIntensity || flickerUnsafe;
            ApplyState(!shouldBeUnsafe);
        }

        public void SetGlobalIntensity(float intensity)
        {
            globalIntensity = Mathf.Clamp01(intensity);
        }

        public void ForceTemporaryUnreality(float seconds)
        {
            forcedUnsafeTimer = Mathf.Max(forcedUnsafeTimer, seconds);
        }

        private void ScheduleNextFlicker()
        {
            Vector2 range = flickerUnsafe ? unsafeDurationRange : safeDurationRange;
            float min = Mathf.Min(range.x, range.y);
            float max = Mathf.Max(range.x, range.y);
            flickerTimer = Random.Range(min, max);
        }

        private void ApplyState(bool shouldBeSolid)
        {
            if (isSolid == shouldBeSolid)
            {
                return;
            }

            isSolid = shouldBeSolid;

            if (platformCollider != null)
            {
                platformCollider.enabled = isSolid;
            }

            if (renderers == null)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!remainVisibleWhenUnsafe)
                {
                    renderer.enabled = isSolid;
                    continue;
                }

                renderer.enabled = true;
                renderer.GetPropertyBlock(block);

                if (isSolid)
                {
                    block.Clear();
                }
                else
                {
                    Color emissive = unsafeTint * unsafeEmissionScale;
                    block.SetColor(BaseColorProperty, unsafeTint);
                    block.SetColor(ColorProperty, unsafeTint);
                    block.SetColor(EmissiveColorProperty, emissive);
                }

                renderer.SetPropertyBlock(block);
            }
        }
    }
}

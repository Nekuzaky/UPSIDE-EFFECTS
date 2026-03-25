using UnityEngine;

namespace Mindrift.UI
{
    public sealed class HUDAltitude : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private Component altitudeTextComponent;

        [Header("Display")]
        [SerializeField] private string prefix = "ALTITUDE";
        [SerializeField] private float updateFrequency = 14f;
        [SerializeField] private string unitLabel = "m";

        private float nextUpdate;

        private void Awake()
        {
            if (playerTransform == null)
            {
                Player.PlayerFallRespawn player = FindFirstObjectByType<Player.PlayerFallRespawn>();
                if (player != null)
                {
                    playerTransform = player.transform;
                }
            }
        }

        private void Update()
        {
            if (playerTransform == null || altitudeTextComponent == null)
            {
                return;
            }

            if (Time.unscaledTime < nextUpdate)
            {
                return;
            }

            nextUpdate = Time.unscaledTime + 1f / Mathf.Max(1f, updateFrequency);
            UITextUtility.SetText(altitudeTextComponent, $"{prefix}: {playerTransform.position.y:000.0} {unitLabel}");
        }
    }
}

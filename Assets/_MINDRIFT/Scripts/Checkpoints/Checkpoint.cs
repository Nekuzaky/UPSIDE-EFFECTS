using UnityEngine;
using Mindrift.Player;

namespace Mindrift.Checkpoints
{
    [RequireComponent(typeof(Collider))]
    public sealed class Checkpoint : MonoBehaviour
    {
        [Header("Checkpoint Data")]
        [SerializeField] private int checkpointIndex;
        [SerializeField] private string checkpointLabel = "Checkpoint";
        [SerializeField] private Transform respawnAnchor;
        [SerializeField] private bool oneShotActivation = true;

        private CheckpointManager checkpointManager;
        private bool hasActivated;

        public int CheckpointIndex => checkpointIndex;
        public string CheckpointLabel => string.IsNullOrWhiteSpace(checkpointLabel) ? name : checkpointLabel;
        public Transform RespawnAnchor => respawnAnchor != null ? respawnAnchor : transform;

        private void Reset()
        {
            Collider trigger = GetComponent<Collider>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
            }
        }

        private void Awake()
        {
            if (respawnAnchor == null)
            {
                respawnAnchor = transform;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasActivated && oneShotActivation)
            {
                return;
            }

            PlayerFallRespawn player = other.GetComponentInParent<PlayerFallRespawn>();
            if (player == null)
            {
                return;
            }

            if (checkpointManager == null)
            {
                checkpointManager = FindFirstObjectByType<CheckpointManager>();
            }

            if (checkpointManager == null)
            {
                return;
            }

            checkpointManager.ActivateCheckpoint(this);
            hasActivated = true;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Vector3 position = RespawnAnchor.position;
            Gizmos.DrawWireSphere(position, 0.4f);
            Gizmos.DrawLine(position, position + Vector3.up * 2f);
        }
    }
}

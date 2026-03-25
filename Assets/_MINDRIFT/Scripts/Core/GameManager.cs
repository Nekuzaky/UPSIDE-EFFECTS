using UnityEngine;

namespace Mindrift.Core
{
    public sealed class GameManager : MonoBehaviour
    {
        [Header("Core References")]
        [SerializeField] private HeightProgressionManager heightProgressionManager;
        [SerializeField] private Checkpoints.CheckpointManager checkpointManager;
        [SerializeField] private Player.PlayerFallRespawn playerFallRespawn;

        [Header("Debug")]
        [SerializeField] private bool autoResolveOnAwake = true;

        public HeightProgressionManager HeightProgressionManager => heightProgressionManager;
        public Checkpoints.CheckpointManager CheckpointManager => checkpointManager;
        public Player.PlayerFallRespawn PlayerFallRespawn => playerFallRespawn;

        private void Awake()
        {
            if (!autoResolveOnAwake)
            {
                return;
            }

            if (heightProgressionManager == null)
            {
                heightProgressionManager = FindFirstObjectByType<HeightProgressionManager>();
            }

            if (checkpointManager == null)
            {
                checkpointManager = FindFirstObjectByType<Checkpoints.CheckpointManager>();
            }

            if (playerFallRespawn == null)
            {
                playerFallRespawn = FindFirstObjectByType<Player.PlayerFallRespawn>();
            }
        }
    }
}

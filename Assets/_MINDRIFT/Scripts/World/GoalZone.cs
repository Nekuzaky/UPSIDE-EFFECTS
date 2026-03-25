using System;
using UnityEngine;
using Mindrift.Player;

namespace Mindrift.World
{
    [RequireComponent(typeof(Collider))]
    public sealed class GoalZone : MonoBehaviour
    {
        [SerializeField] private bool oneShot = true;
        [SerializeField] private string requiredTag = "Player";

        private bool reached;

        public event Action GoalReached;

        private void Reset()
        {
            Collider trigger = GetComponent<Collider>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (oneShot && reached)
            {
                return;
            }

            if (!string.IsNullOrEmpty(requiredTag))
            {
                if (!other.CompareTag(requiredTag) && !other.transform.root.CompareTag(requiredTag))
                {
                    PlayerFallRespawn playerFallback = other.GetComponentInParent<PlayerFallRespawn>();
                    if (playerFallback == null)
                    {
                        return;
                    }
                }
            }

            reached = true;
            GoalReached?.Invoke();
        }
    }
}

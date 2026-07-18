using UnityEngine;

namespace TheLastEmpire
{
    public class ZombieAI : BaseEnemyAI
    {
        public enum ZombieType
        {
            Normal,
            Dog,
            Boomer,
            Leader,
            Runner
        }

        [Header("Zombie Settings")]
        [SerializeField] private ZombieType zombieType = ZombieType.Normal;

        protected override void Start()
        {
            base.Start();

            // Specialize stats depending on ZombieType
            switch (zombieType)
            {
                case ZombieType.Dog:
                    moveSpeed = 5f;
                    detectionRange = 7f;
                    break;
                case ZombieType.Runner:
                    moveSpeed = 4f;
                    break;
                case ZombieType.Leader:
                    moveSpeed = 2.5f;
                    detectionRange = 6f;
                    break;
                case ZombieType.Boomer:
                    moveSpeed = 2f;
                    break;
            }
        }

        protected override void UpdateAIBehavior()
        {
            bool isNight = DayNightManager.Instance != null && DayNightManager.Instance.IsNight;
            bool detected = IsPlayerInDetectionRange();

            if (detected)
            {
                currentState = AIState.Chase;
                ChasePlayer();
            }
            else
            {
                if (isNight)
                {
                    // GDD: Zombie will not wander during the night (stands still / idle)
                    currentState = AIState.Idle;
                    rb.linearVelocity = Vector2.zero;
                }
                else
                {
                    // Day: Wander around normally
                    WanderMovement();
                }
            }
        }
    }
}

using UnityEngine;

namespace TheLastEmpire
{
    public class AlienAI : BaseEnemyAI
    {
        public enum AlienType
        {
            Normal,
            Hook,
            Invisible,
            Smart,
            Leader
        }

        [Header("Alien Settings")]
        [SerializeField] private AlienType alienType = AlienType.Normal;

        protected override void Start()
        {
            base.Start();

            // Specialize Alien stats
            switch (alienType)
            {
                case AlienType.Hook:
                    moveSpeed = 4.5f;
                    detectionRange = 8f;
                    break;
                case AlienType.Invisible:
                    moveSpeed = 3.5f;
                    detectionRange = 4f;
                    break;
                case AlienType.Smart:
                    moveSpeed = 4f;
                    detectionRange = 10f;
                    break;
            }
        }

        protected override void UpdateAIBehavior()
        {
            bool isNight = DayNightManager.Instance != null && DayNightManager.Instance.IsNight;

            if (isNight)
            {
                // GDD: Alien always charges/chases player globally at night!
                currentState = AIState.Chase;
                ChasePlayer(1.2f); // 20% speed boost at night!
            }
            else
            {
                // Day: behaves normally (wanders, chases if within range)
                bool detected = IsPlayerInDetectionRange();
                if (detected)
                {
                    currentState = AIState.Chase;
                    ChasePlayer();
                }
                else
                {
                    WanderMovement();
                }
            }
        }
    }
}

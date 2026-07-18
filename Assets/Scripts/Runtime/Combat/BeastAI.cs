using UnityEngine;

namespace TheLastEmpire
{
    public class BeastAI : BaseEnemyAI
    {
        [Header("Beast Settings")]
        [SerializeField] private float enrageSpeedMultiplier = 1.5f;

        protected override void UpdateAIBehavior()
        {
            bool detected = IsPlayerInDetectionRange();

            if (detected)
            {
                currentState = AIState.Chase;
                
                // Enrage speed boost
                ChasePlayer(enrageSpeedMultiplier);
            }
            else
            {
                WanderMovement();
            }
        }
    }
}

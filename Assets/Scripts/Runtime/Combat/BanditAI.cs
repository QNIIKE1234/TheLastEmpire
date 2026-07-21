using UnityEngine;

namespace TheLastEmpire
{
    public class BanditAI : BaseEnemyAI
    {
        [Header("Bandit Settings")]
        [SerializeField] private float fireDistance = 6f;

        protected override void UpdateAIBehavior()
        {
            bool detected = IsPlayerInDetectionRange();

            if (detected)
            {
                float dist = Vector3.Distance(transform.position, playerTransform.position);
                if (dist > fireDistance)
                {
                    currentState = AIState.Chase;
                    ChasePlayer();
                }
                else
                {
                    // Stand still and fire / cover
                    currentState = AIState.Idle;
                    rb.linearVelocity = Vector3.zero;
                    
                    // Aim at player
                    Vector3 direction = (playerTransform.position - transform.position);
                    direction.y = 0f;
                    direction.Normalize();
                    if (direction.sqrMagnitude > 0.01f)
                    {
                        transform.forward = direction;
                    }
                }
            }
            else
            {
                WanderMovement();
            }
        }
    }
}

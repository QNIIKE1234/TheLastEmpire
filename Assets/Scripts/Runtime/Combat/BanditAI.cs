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
                float dist = Vector2.Distance(transform.position, playerTransform.position);
                if (dist > fireDistance)
                {
                    currentState = AIState.Chase;
                    ChasePlayer();
                }
                else
                {
                    // Stand still and fire / cover
                    currentState = AIState.Idle;
                    rb.linearVelocity = Vector2.zero;
                    
                    // Aim at player
                    Vector2 direction = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
                    float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
                }
            }
            else
            {
                WanderMovement();
            }
        }
    }
}

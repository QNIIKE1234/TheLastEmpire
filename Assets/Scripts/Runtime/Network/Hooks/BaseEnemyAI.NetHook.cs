using UnityEngine;
using TheLastEmpire.Network;

namespace TheLastEmpire
{
    /// <summary>
    /// BaseEnemyAI.NetHook — ส่วนขยาย partial class สำหรับระบบศัตรูในโหมด Co-op 1–6 คน
    /// - Host-Authoritative AI: คำนวณการเดินและเลือกเป้าหมายเฉพาะบนเครื่อง Host (หรือโหมด Offline)
    /// - Multi-Target Aggro: อัปเดตเป้าหมายทุกๆ 0.3 วินาที โดยเลือกผู้เล่นที่มีชีวิตและอยู่ใกล้ที่สุดจาก PlayerRegistry
    /// - Client Puppet Mode: บนเครื่อง Client จะปิดการคิด AI เองและเคลื่อนที่ตามพิกัดที่ซิงค์มาจาก Host 100%
    /// </summary>
    public abstract partial class BaseEnemyAI
    {
        public bool IsNetworkPuppet { get; set; } = false;
        public int NetworkEnemyId { get; set; } = -1;

        private float _multiplayerAggroRefreshTimer = 0f;
        private Vector3 _puppetTargetPosition;
        private float _puppetTargetRotY;
        private Vector3 _puppetVelocity;
        private bool _hasPuppetTarget = false;

        protected void UpdateMultiplayerTargetAggro()
        {
            _multiplayerAggroRefreshTimer -= Time.fixedDeltaTime;
            if (_multiplayerAggroRefreshTimer > 0f && playerTransform != null && !IsTargetDead())
            {
                return;
            }

            _multiplayerAggroRefreshTimer = 0.30f; // รีเฟรชหาผู้เล่นที่ใกล้ที่สุดทุก 0.3 วินาทีตามแผน Section 4.4
            PlayerController closest = PlayerRegistry.GetClosestAlivePlayer(transform.position);
            if (closest != null)
            {
                playerTransform = closest.transform;
                playerHealth = closest.PlayerHealth ?? closest.GetComponent<Health>();
            }
        }

        public void ApplyNetworkPuppetState(EnemyStateNetPayload state)
        {
            IsNetworkPuppet = true;
            NetworkEnemyId = state.enemyNetId;
            _puppetTargetPosition = new Vector3(state.posX, transform.position.y, state.posZ);
            _puppetTargetRotY = state.rotY;
            _puppetVelocity = new Vector3(state.velX, 0f, state.velZ);
            currentState = (AIState)state.aiState;
            _hasPuppetTarget = true;

            if (state.isChaserPhasing && !_isChaserPhasing)
            {
                EnableChaserPhasing(playerTransform, 4.5f);
            }
            else if (!state.isChaserPhasing && _isChaserPhasing)
            {
                DisableChaserPhasing();
            }

            if (health != null && state.maxHealth > 0f)
            {
                health.SetHealthFromNetwork(state.currentHealth, state.maxHealth);
            }
        }

        protected void FixedUpdateNetworkPuppet()
        {
            if (!_hasPuppetTarget) return;

            Vector3 cur = transform.position;
            Vector3 target = new Vector3(_puppetTargetPosition.x, _isChaserPhasing ? _phasingGroundY : cur.y, _puppetTargetPosition.z);
            transform.position = Vector3.Lerp(cur, target, Time.fixedDeltaTime * 15f);

            Quaternion targetRot = Quaternion.Euler(0f, _puppetTargetRotY, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.fixedDeltaTime * 16f);

            if (rb != null)
            {
                rb.linearVelocity = new Vector3(_puppetVelocity.x, _isChaserPhasing ? 0f : rb.linearVelocity.y, _puppetVelocity.z);
            }

            UpdateEnemyAnimator();
        }
    }
}

using System;
using UnityEngine;
using TMPro;

namespace TheLastEmpire
{
    /// <summary>
    /// FleeingSurvivorRunner — ควบคุม NPC ที่วิ่งหนีฝูงศัตรูข้ามฉาก
    /// วิ่งจากทิศตรงข้าม ผ่านตัว Player ไปยังทิศที่ Player วาปเข้ามา พร้อมลากฝูงศัตรูตามหลังมา
    /// </summary>
    public class FleeingSurvivorRunner : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 5.2f;

        private Vector3 _destination;
        private Action _onEscaped;
        private Animator _animator;
        private TMP_Text _speechText;
        private float _shoutTimer = 0f;
        private bool _isInitialized = false;

        private static readonly string[] PanickedShouts = new string[]
        {
            "RUN FOR YOUR LIFE!!",
            "THEY'RE RIGHT BEHIND ME!!",
            "HELP! GET OUT OF THE WAY!!",
            "DON'T JUST STAND THERE, RUN!!",
            "THERE ARE TOO MANY OF THEM!!"
        };

        public void Initialize(Vector3 destination, Action onEscaped = null, float speed = 5.2f)
        {
            _destination = destination;
            _onEscaped = onEscaped;
            moveSpeed = speed;

            // ปิด NPCController ปกติเพื่อไม่ให้ trigger การนั่งหรือเปิดหน้าต่างร้านค้า
            NPCController npcController = GetComponent<NPCController>();
            if (npcController != null)
            {
                npcController.enabled = false;
            }

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }

            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            // หา Animator และสั่งท่าวิ่งทันที
            _animator = GetComponentInChildren<Animator>();
            if (_animator != null)
            {
                _animator.applyRootMotion = false;
                _animator.SetBool("IsSitting", false);
                _animator.SetBool("IsRunning", true);
                _animator.SetFloat("Speed", moveSpeed);
                _animator.CrossFadeInFixedTime("Run", 0.1f, 0);
            }

            // หา Floating Text เพื่อแสดงข้อความร้องขอความช่วยเหลือ
            _speechText = GetComponentInChildren<TMP_Text>();
            ShoutRandom();

            _isInitialized = true;
        }

        private void Update()
        {
            if (!_isInitialized) return;

            Vector3 currentPos = transform.position;
            Vector3 diff = _destination - currentPos;
            diff.y = 0f;
            float dist = diff.magnitude;

            if (dist > 0.05f)
            {
                Vector3 dir = diff / dist;
                transform.position += dir * (moveSpeed * Time.deltaTime);

                if (dir.sqrMagnitude > 0.001f)
                {
                    transform.forward = Vector3.Slerp(transform.forward, dir, Time.deltaTime * 12f);
                }
            }

            // อัปเดตข้อความตะโกนเป็นระยะ
            _shoutTimer += Time.deltaTime;
            if (_shoutTimer >= 1.4f)
            {
                _shoutTimer = 0f;
                ShoutRandom();
            }

            // เมื่อวิ่งไปถึงจุดหมาย (หรือหลุดออกนอกขอบประตู) ให้ถือว่าหนีรอดและหายตัวไป
            if (dist <= 1.2f)
            {
                EscapeAndDespawn();
            }
        }

        private void ShoutRandom()
        {
            if (_speechText != null)
            {
                string msg = PanickedShouts[UnityEngine.Random.Range(0, PanickedShouts.Length)];
                _speechText.text = $"<color=#FFAA00>Maya:</color> <color=#FF2222><b>{msg}</b></color>";
            }
        }

        private void EscapeAndDespawn()
        {
            _isInitialized = false;
            _onEscaped?.Invoke();
            Destroy(gameObject);
        }
    }
}


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
                rb.detectCollisions = false;
            }

            // บังคับ Collider ทุกชิ้นในตัวและลูกๆ เป็น Trigger 100% เพื่อไม่ให้ชนติดตึก กำแพง หรือสิ่งกีดขวางใดๆ
            Collider[] allCols = GetComponentsInChildren<Collider>();
            foreach (var c in allCols)
            {
                if (c != null) c.isTrigger = true;
            }

            // จัดหน้าตา ModelRoot ให้หันหน้าตรงไปข้างหน้า (แก้ปัญหาโมเดล NPC เดิมกลับหลัง 180 องศา)
            Transform modelRoot = transform.Find("ModelRoot");
            if (modelRoot != null)
            {
                modelRoot.localRotation = Quaternion.identity;
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
            if (_speechText != null)
            {
                _speechText.gameObject.SetActive(true);
                _speechText.fontSize = 4.5f;
            }
            ShoutRandom();

            _isInitialized = true;
        }

        private float _lifetime = 0f;

        private void Update()
        {
            if (!_isInitialized) return;

            _lifetime += Time.deltaTime;

            Vector3 currentPos = transform.position;
            currentPos.y = _destination.y; // ล็อคระดับความสูงบนพื้น ไม่ให้จมดิน
            Vector3 diff = _destination - currentPos;
            diff.y = 0f;
            float dist = diff.magnitude;

            if (dist > 0.05f)
            {
                Vector3 dir = diff / dist;
                transform.position = currentPos + dir * (moveSpeed * Time.deltaTime);

                if (dir.sqrMagnitude > 0.001f)
                {
                    transform.forward = Vector3.Slerp(transform.forward, dir, Time.deltaTime * 12f);
                }
            }

            // อัปเดตข้อความตะโกนเป็นระยะ
            _shoutTimer += Time.deltaTime;
            if (_shoutTimer >= 1.2f)
            {
                _shoutTimer = 0f;
                ShoutRandom();
            }

            // เมื่อวิ่งพ้นขอบประตูออกไปแล้ว หรือหมดเวลา ให้ถือว่าหนีรอดและหายตัวไป
            if (dist <= 1.2f || _lifetime > 25f)
            {
                EscapeAndDespawn();
            }
        }

        private void ShoutRandom()
        {
            if (_speechText != null)
            {
                string msg = PanickedShouts[UnityEngine.Random.Range(0, PanickedShouts.Length)];
                _speechText.text = $"<color=#FFCC00><b>MAYA (SCOUT):</b></color>\n<color=#FF2222><b>\"{msg}\"</b></color>";
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


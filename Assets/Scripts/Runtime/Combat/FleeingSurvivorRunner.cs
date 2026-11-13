using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace TheLastEmpire
{
    /// <summary>
    /// FleeingSurvivorRunner — ควบคุม NPC (Maya) ที่วิ่งหนีฝูงศัตรูข้ามฉาก
    /// เกิดที่ทิศตรงข้าม วิ่งตรงเข้าหาตัว Player ผ่านตัว Player และทะลุออกประตูที่ Player เพิ่งเข้ามา
    /// </summary>
    public class FleeingSurvivorRunner : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 4.0f;

        [Header("Dialog Bubble Reference")]
        [Tooltip("กล่อง Panel บทพูดลอยเหนือหัว (ลาก DIalogPanel จากใต้ Canvas มาใส่)")]
        [SerializeField] private GameObject dialogPanel;
        [Tooltip("Text (TMP) สำหรับแสดงข้อความในกล่องบทพูดลอย (ลาก Text (TMP) มาใส่)")]
        [SerializeField] private TMP_Text dialogText;
        [Tooltip("Image สำหรับแสดงไอคอนตัวละครในกล่องบทพูดลอย (ลาก Image ช่องกระเป๋า/ไอคอนมาใส่)")]
        [SerializeField] private UnityEngine.UI.Image dialogIcon;

        private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
        private static readonly int IsSittingHash = Animator.StringToHash("IsSitting");
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int RunStateHash = Animator.StringToHash("Run");

        private Transform _playerTransform;
        private Vector3 _exitDestination;
        private Vector3 _runAxis;
        private Action _onEscaped;
        private readonly List<Animator> _animators = new List<Animator>();
        private int _animatorRefreshFrames = 0;
        private float _shoutTimer = 0f;
        private bool _isInitialized = false;
        private bool _hasPassedPlayer = false;
        private float _lifetime = 0f;

        private static readonly string[] PanickedShouts = new string[]
        {
            "Run and save your life!!",
            "They're right behind me!!",
            "Help! Get out of the way!!",
            "Don't just stand there, run!!",
            "There are too many of them!!"
        };

        public void Initialize(Transform playerTransform, Vector3 exitDestination, Action onEscaped = null, float speed = 4.0f, Sprite icon = null)
        {
            _playerTransform = playerTransform;
            _exitDestination = exitDestination;
            _onEscaped = onEscaped;
            moveSpeed = speed;
            _hasPassedPlayer = false;

            Vector3 axis = exitDestination - transform.position;
            axis.y = 0f;
            _runAxis = axis.sqrMagnitude > 0.001f ? axis.normalized : transform.forward;

            // ดึง Ref Dialog จาก NPCController ก่อนที่จะทำลายคอมโพเนนต์
            NPCController npcController = GetComponent<NPCController>();
            if (npcController != null)
            {
                if (dialogPanel == null && npcController.DialogPanel != null) dialogPanel = npcController.DialogPanel;
                if (dialogText == null && npcController.DialogText != null) dialogText = npcController.DialogText;
                if (dialogIcon == null && npcController.DialogIcon != null) dialogIcon = npcController.DialogIcon;

                if (icon == null && npcController.NPCData != null)
                {
                    icon = npcController.NPCData.npcIcon != null ? npcController.NPCData.npcIcon : npcController.NPCData.npcPortrait;
                }

                npcController.CanInteract = false;
                npcController.enabled = false;
                Destroy(npcController); // ทำลายทิ้งทันที หมดโอกาส Interact 100%
            }

            // ค้นหา Dialog Panel & Text & Icon อัตโนมัติ หากยังไม่ได้ลากใส่ใน Inspector
            if (dialogPanel == null)
            {
                Transform found = transform.Find("DIalogPanel") ?? 
                                  transform.Find("DialogPanel") ?? 
                                  transform.Find("Canvas/DIalogPanel") ?? 
                                  transform.Find("Canvas/DialogPanel");
                if (found != null) dialogPanel = found.gameObject;
            }

            if (dialogText == null && dialogPanel != null)
            {
                dialogText = dialogPanel.GetComponentInChildren<TMP_Text>();
            }

            if (dialogText == null)
            {
                dialogText = GetComponentInChildren<TMP_Text>();
            }

            if (dialogIcon == null && dialogPanel != null)
            {
                UnityEngine.UI.Image[] images = dialogPanel.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                foreach (var img in images)
                {
                    if (img.gameObject != dialogPanel && (img.gameObject.name.Contains("Icon") || img.gameObject.name.Contains("Image")))
                    {
                        dialogIcon = img;
                        break;
                    }
                }
            }

            if (dialogPanel != null)
            {
                dialogPanel.SetActive(true);
            }
            if (dialogText != null)
            {
                dialogText.gameObject.SetActive(true);
            }
            if (dialogIcon != null && icon != null)
            {
                dialogIcon.sprite = icon;
                dialogIcon.gameObject.SetActive(true);
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

            // ApplyData() เพิ่งเปลี่ยนโมเดลเป็น Maya และสั่ง Destroy โมเดลเก่า (Destroy จะเกิดตอนจบเฟรม)
            // จึงต้องค้นหา Animator ใหม่อีกครั้งในเฟรมถัดๆ ไป เพื่อให้ได้ Animator ของโมเดล Maya ตัวจริง
            _animatorRefreshFrames = 10;
            RefreshAnimators();
            DriveRunAnimation();

            ShoutRandom();

            _isInitialized = true;
        }

        private void RefreshAnimators()
        {
            _animators.Clear();

            // ดึง Controller สำรองจาก Player ทันที เพื่อความเร็วและความแน่นอน 100%
            RuntimeAnimatorController fallbackCtrl = null;
            if (PlayerController.Instance != null)
            {
                Animator pAnim = PlayerController.Instance.GetComponentInChildren<Animator>();
                if (pAnim != null) fallbackCtrl = pAnim.runtimeAnimatorController;
            }
            if (fallbackCtrl == null)
            {
                fallbackCtrl = AddressableManager.LoadAsset<RuntimeAnimatorController>("Image/Animation/Chibi_Player_Controller");
            }
            if (fallbackCtrl == null)
            {
                fallbackCtrl = AddressableManager.LoadAsset<RuntimeAnimatorController>("Animation/Chibi_Player_Controller");
            }

            Animator[] found = GetComponentsInChildren<Animator>(false);
            foreach (var anim in found)
            {
                if (anim == null) continue;
                anim.applyRootMotion = false;

                if (anim.runtimeAnimatorController == null && fallbackCtrl != null)
                {
                    anim.runtimeAnimatorController = fallbackCtrl;
                }

                if (anim.runtimeAnimatorController != null)
                {
                    _animators.Add(anim);
                }
            }
        }

        /// <summary>
        /// บังคับท่าวิ่งทุกเฟรม: ตั้งค่า Parameter และ CrossFade เข้า State "Run" ทันทีถ้ายังไม่ได้อยู่ในท่าวิ่ง
        /// </summary>
        private void DriveRunAnimation()
        {
            for (int i = _animators.Count - 1; i >= 0; i--)
            {
                Animator anim = _animators[i];
                if (anim == null || !anim.isActiveAndEnabled)
                {
                    _animators.RemoveAt(i);
                    continue;
                }

                anim.SetBool(IsSittingHash, false);
                anim.SetBool(IsRunningHash, true);
                anim.SetFloat(SpeedHash, moveSpeed);

                AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
                if (!state.IsName("Run") && !anim.IsInTransition(0))
                {
                    anim.CrossFadeInFixedTime("Run", 0.05f, 0);
                }
            }
        }

        private void Update()
        {
            if (!_isInitialized) return;

            _lifetime += Time.deltaTime;

            if (_animatorRefreshFrames > 0 || _animators.Count == 0)
            {
                if (_animatorRefreshFrames > 0) _animatorRefreshFrames--;
                RefreshAnimators();
            }
            DriveRunAnimation();

            Vector3 currentPos = transform.position;
            currentPos.y = _exitDestination.y; // ล็อคระดับความสูงบนพื้น ไม่ให้จมดิน

            // ระยะแรก: วิ่งตรงเข้าหาตัว Player
            // เมื่อถึงตัว Player หรือวิ่งเลยตำแหน่ง Player ตามแนววิ่งแล้ว: วิ่งทะลุออกประตูหลัง Player (_exitDestination)
            Vector3 targetPos = _exitDestination;
            if (!_hasPassedPlayer && _playerTransform != null)
            {
                Vector3 playerPos = _playerTransform.position;
                playerPos.y = currentPos.y;

                Vector3 toPlayer = playerPos - currentPos;
                bool reachedPlayer = toPlayer.magnitude <= 1.2f;
                bool overtookPlayer = Vector3.Dot(toPlayer, _runAxis) <= 0f; // Player อยู่ข้างหลัง Maya แล้ว

                if (reachedPlayer || overtookPlayer)
                {
                    _hasPassedPlayer = true;
                }
                else
                {
                    targetPos = playerPos;
                }
            }

            Vector3 diff = targetPos - currentPos;
            diff.y = 0f;
            float dist = diff.magnitude;

            if (dist > 0.05f)
            {
                Vector3 dir = diff / dist;
                float step = Mathf.Min(moveSpeed * Time.deltaTime, dist);
                transform.position = currentPos + dir * step;
                transform.forward = Vector3.Slerp(transform.forward, dir, Time.deltaTime * 12f);
            }

            // อัปเดตข้อความตะโกนเป็นระยะ
            _shoutTimer += Time.deltaTime;
            if (_shoutTimer >= 1.2f)
            {
                _shoutTimer = 0f;
                ShoutRandom();
            }

            // เมื่อวิ่งถึงจุดออก หรือหมดเวลา ให้ถือว่าหนีรอดและหายตัวไป
            if ((_hasPassedPlayer && dist <= 1.2f) || _lifetime > 20f)
            {
                EscapeAndDespawn();
            }
        }

        private void ShoutRandom()
        {
            if (dialogText != null)
            {
                string msg = PanickedShouts[UnityEngine.Random.Range(0, PanickedShouts.Length)];
                dialogText.text = msg;
            }
            if (dialogPanel != null)
            {
                dialogPanel.SetActive(true);
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

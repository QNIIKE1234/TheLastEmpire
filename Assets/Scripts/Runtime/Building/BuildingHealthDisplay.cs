using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TheLastEmpire
{
    /// <summary>
    /// BuildingHealthDisplay — แสดงหลอดเลือด HP เหนือสิ่งปลูกสร้างแบบ World-Space Billboard
    /// คุณสมบัติ:
    /// - แสดงผลอัตโนมัติเมื่อสิ่งปลูกสร้างได้รับความเสียหาย (CurrentHealth < MaxHealth)
    /// - เปลี่ยนสีหลอดเลือดตามสถานะ: เขียว (>50%) -> เหลือง (25-50%) -> แดง (<25%)
    /// - มีตัวเลขแสดงค่า HP ปัจจุบัน / สูงสุด
    /// - เมื่อซ่อมแซมจนเลือดเต็ม จะแสดงผล 2 วินาทีแล้วซ่อนตัวเองอัตโนมัติเพื่อไม่ให้รกหน้าจอ
    /// - หมุนหันหน้าเข้าหากล้องเสมอ (Billboard)
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class BuildingHealthDisplay : MonoBehaviour
    {
        private BuildingObject _building;
        private Canvas _canvas;
        private GameObject _canvasRoot;
        private Image _fillImage;
        private TMP_Text _hpText;
        private Coroutine _hideCoroutine;

        [Header("Display Settings")]
        [SerializeField] private float yOffsetPadding = 0.35f;
        [SerializeField] private float hideDelayAfterFull = 2.0f;

        private static readonly Color GreenColor = new Color(0.18f, 0.8f, 0.44f, 0.95f);
        private static readonly Color YellowColor = new Color(0.95f, 0.77f, 0.06f, 0.95f);
        private static readonly Color RedColor = new Color(0.91f, 0.3f, 0.24f, 0.95f);

        private void Awake()
        {
            _building = GetComponent<BuildingObject>();
        }

        private void Start()
        {
            SetupCanvas();

            if (_building != null)
            {
                _building.OnHealthChanged += HandleHealthChanged;
                // อัปเดตสถานะเริ่มต้น
                HandleHealthChanged(_building.CurrentHealth, _building.MaxHealth);
            }
        }

        private void OnDestroy()
        {
            if (_building != null)
            {
                _building.OnHealthChanged -= HandleHealthChanged;
            }
        }

        private void SetupCanvas()
        {
            if (_canvasRoot != null) return;

            // 1. คำนวณความสูงของหลอดเลือดให้อยู่เหนือตัวสิ่งปลูกสร้าง
            float height = 1.8f;
            if (_building != null && _building.Data != null)
            {
                height = _building.Data.footprintSize.y + yOffsetPadding;
            }
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                height = Mathf.Max(height, col.bounds.size.y + yOffsetPadding);
            }

            // 2. สร้าง Root GameObject สำหรับ Canvas
            _canvasRoot = new GameObject("HealthBar_Canvas");
            _canvasRoot.transform.SetParent(transform, false);
            _canvasRoot.transform.localPosition = new Vector3(0f, height, 0f);

            _canvas = _canvasRoot.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 200;

            if (Camera.main != null)
            {
                _canvas.worldCamera = Camera.main;
            }

            // หมุนหันหน้าเข้าหากล้องเสมอ
            _canvasRoot.AddComponent<BillboardUI>();

            RectTransform canvasRt = _canvasRoot.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(100f, 18f);
            canvasRt.localScale = Vector3.one * 0.012f; // กว้างประมาณ 1.2 เมตรในโลก 3D

            // 3. พื้นหลังแถบเลือด (Background)
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(_canvasRoot.transform, false);
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.08f, 0.08f, 0.08f, 0.85f);
            RectTransform bgRt = bgObj.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;

            // 4. แถบสีเลือด (Fill)
            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(_canvasRoot.transform, false);
            _fillImage = fillObj.AddComponent<Image>();
            _fillImage.type = Image.Type.Filled;
            _fillImage.fillMethod = Image.FillMethod.Horizontal;
            _fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            _fillImage.color = GreenColor;
            RectTransform fillRt = fillObj.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0.02f, 0.12f);
            fillRt.anchorMax = new Vector2(0.98f, 0.88f);
            fillRt.sizeDelta = Vector2.zero;

            // 5. ข้อความตัวเลข HP
            GameObject textObj = new GameObject("HP_Text");
            textObj.transform.SetParent(_canvasRoot.transform, false);
            _hpText = textObj.AddComponent<TextMeshProUGUI>();
            _hpText.alignment = TextAlignmentOptions.Center;
            _hpText.fontSize = 11;
            _hpText.fontStyle = FontStyles.Bold;
            _hpText.color = Color.white;
            _hpText.raycastTarget = false;
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;

            // ค่าเริ่มต้นให้ซ่อนไว้ก่อน ถ้าเลือดยังเต็ม
            _canvasRoot.SetActive(false);
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (_canvasRoot == null)
            {
                SetupCanvas();
            }

            if (max <= 0f) return;

            float ratio = Mathf.Clamp01(current / max);

            if (_fillImage != null)
            {
                _fillImage.fillAmount = ratio;

                // เปลี่ยนสีตามระดับ HP
                if (ratio > 0.5f)
                {
                    _fillImage.color = Color.Lerp(YellowColor, GreenColor, (ratio - 0.5f) * 2f);
                }
                else
                {
                    _fillImage.color = Color.Lerp(RedColor, YellowColor, ratio * 2f);
                }
            }

            if (_hpText != null)
            {
                _hpText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
            }

            // ถ้าเลือดลดต่ำกว่า Max: แสดงหลอดเลือดทันที
            if (current < max)
            {
                if (_hideCoroutine != null)
                {
                    StopCoroutine(_hideCoroutine);
                    _hideCoroutine = null;
                }
                _canvasRoot.SetActive(true);
            }
            else
            {
                // ถ้าเลือดเต็ม 100%: รอ 2 วินาทีแล้วค่อยซ่อน
                if (_canvasRoot.activeSelf && gameObject.activeInHierarchy)
                {
                    if (_hideCoroutine != null)
                    {
                        StopCoroutine(_hideCoroutine);
                    }
                    _hideCoroutine = StartCoroutine(HideAfterDelay());
                }
            }
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(hideDelayAfterFull);
            if (_canvasRoot != null)
            {
                _canvasRoot.SetActive(false);
            }
            _hideCoroutine = null;
        }
    }
}

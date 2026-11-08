using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TheLastEmpire
{
    /// <summary>
    /// พาเนลแสดงรายละเอียดไอเทม/สูตรคราฟต์ที่เลือกอยู่ฝั่งขวา (Craft Description Panel)
    /// แสดง: ไอคอนใหญ่ (Image) + ชื่อไอเทม (TMP_Text) + คำอธิบาย (TMP_Text)
    /// </summary>
    public class CraftDescriptionPanel : MonoBehaviour
    {
        [Header("Root Panel")]
        [Tooltip("GameObject หน้าต่าง Description Panel (ใช้เปิด/ปิด หรือซ่อนเมื่อไม่มีไอเทมเลือก)")]
        [SerializeField] private GameObject panelRoot;

        [Header("UI Display Elements")]
        [Tooltip("Image สำหรับแสดงรูปไอคอนขนาดใหญ่ของไอเทมที่เลือก")]
        [SerializeField] private Image itemIconImage;

        [Tooltip("TextMeshPro สำหรับแสดงชื่อไอเทม")]
        [SerializeField] private TMP_Text itemNameText;

        [Tooltip("TextMeshPro สำหรับแสดงคำอธิบายคุณสมบัติ รายละเอียดของไอเทม")]
        [SerializeField] private TMP_Text itemDescriptionText;

        [Header("Default / Fallback Settings")]
        [Tooltip("ข้อความคำอธิบายเริ่มต้นหากไอเทมไม่มีคำอธิบาย")]
        [SerializeField] private string defaultDescription = "ไม่มีข้อมูลคำอธิบายสำหรับไอเทมนี้";

        [Tooltip("เปิด/ปิด Image component อัตโนมัติเมื่อไม่มี Sprite")]
        [SerializeField] private bool hideIconIfEmpty = false;

        private CraftingRecipe _currentRecipe;
        public CraftingRecipe CurrentRecipe => _currentRecipe;

        private void Awake()
        {
            AutoBindIfNeeded();
        }

#if UNITY_EDITOR
        private void Reset()
        {
            AutoBindIfNeeded();
        }
#endif

        /// <summary>
        /// ค้นหา Component ภายใน Hierarchy อัตโนมัติเมื่อกด Reset หรือ Context Menu (ไม่เรียกใช้ FindObjectOfType ตอน Runtime)
        /// </summary>
        [ContextMenu("Auto Bind UI Elements")]
        public void AutoBindIfNeeded()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            if (itemIconImage == null)
            {
                Image[] images = GetComponentsInChildren<Image>(true);
                foreach (Image img in images)
                {
                    if (img.gameObject != gameObject && 
                        (img.name.IndexOf("Icon", StringComparison.OrdinalIgnoreCase) >= 0 || 
                         img.name.Equals("Image", StringComparison.OrdinalIgnoreCase)))
                    {
                        itemIconImage = img;
                        break;
                    }
                }

                if (itemIconImage == null && images.Length > 1)
                {
                    itemIconImage = images[1];
                }
            }

            if (itemNameText == null || itemDescriptionText == null)
            {
                TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
                foreach (TMP_Text txt in texts)
                {
                    string n = txt.gameObject.name;
                    if (itemNameText == null && (n.Equals("Name", StringComparison.OrdinalIgnoreCase) || n.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        itemNameText = txt;
                    }
                    else if (itemDescriptionText == null && (n.IndexOf("Desc", StringComparison.OrdinalIgnoreCase) >= 0 || n.Equals("Name (1)", StringComparison.OrdinalIgnoreCase)))
                    {
                        itemDescriptionText = txt;
                    }
                }

                if (itemNameText == null && texts.Length > 0)
                {
                    itemNameText = texts[0];
                }
                if (itemDescriptionText == null && texts.Length > 1)
                {
                    itemDescriptionText = texts[1];
                }
            }
        }

        /// <summary>
        /// แสดงผลข้อมูลของสูตรคราฟต์ที่เลือก
        /// </summary>
        public void DisplayRecipe(CraftingRecipe recipe)
        {
            _currentRecipe = recipe;

            if (recipe == null)
            {
                Clear();
                return;
            }

            if (panelRoot != null && !panelRoot.activeSelf)
            {
                panelRoot.SetActive(true);
            }

            // 1. ดึงชื่อไอเทม
            string displayName = !string.IsNullOrEmpty(recipe.recipeName)
                ? recipe.recipeName
                : recipe.ResolvedResultName;

            if (itemNameText != null)
            {
                itemNameText.text = displayName;
            }

            // 2. ดึงไอคอน
            Sprite icon = recipe.ResolvedResultIcon;
            if (icon == null && BuildingManager.Instance != null)
            {
                BuildingData bData = BuildingManager.Instance.GetBuildingDataForRecipe(recipe);
                if (bData != null && bData.icon != null)
                {
                    icon = bData.icon;
                }
            }

            if (itemIconImage != null)
            {
                itemIconImage.sprite = icon;
                if (hideIconIfEmpty)
                {
                    itemIconImage.enabled = icon != null;
                }
                else
                {
                    itemIconImage.color = icon != null ? Color.white : new Color(1f, 1f, 1f, 0f);
                }
            }

            // 3. ดึงคำอธิบาย
            string desc = recipe.description;
            if (string.IsNullOrEmpty(desc) && recipe.resultItem != null && !string.IsNullOrEmpty(recipe.resultItem.description))
            {
                desc = recipe.resultItem.description;
            }
            if (string.IsNullOrEmpty(desc) && BuildingManager.Instance != null)
            {
                BuildingData bData = BuildingManager.Instance.GetBuildingDataForRecipe(recipe);
                if (bData != null && !string.IsNullOrEmpty(bData.description))
                {
                    desc = bData.description;
                }
            }
            if (string.IsNullOrEmpty(desc))
            {
                desc = defaultDescription;
            }

            if (itemDescriptionText != null)
            {
                itemDescriptionText.text = desc;
            }
        }

        /// <summary>
        /// ล้างข้อมูลหรือซ่อน UI
        /// </summary>
        public void Clear()
        {
            _currentRecipe = null;

            if (itemNameText != null)
            {
                itemNameText.text = string.Empty;
            }

            if (itemDescriptionText != null)
            {
                itemDescriptionText.text = string.Empty;
            }

            if (itemIconImage != null)
            {
                itemIconImage.sprite = null;
                if (hideIconIfEmpty)
                {
                    itemIconImage.enabled = false;
                }
                else
                {
                    itemIconImage.color = new Color(1f, 1f, 1f, 0f);
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TheLastEmpire
{
    /// <summary>
    /// โครงสร้าง UI สำหรับช่องแสดงวัตถุดิบ 1 ชิ้นในแถวคราฟต์ (มีสูงสุด 5 ช่องต่อ 1 CraftThumbnail)
    /// </summary>
    [Serializable]
    public class CraftIngredientSlotUI
    {
        [Tooltip("GameObject หลักของช่องวัตถุดิบนี้ (ใช้เปิด/ปิดตามจำนวนวัตถุดิบที่ใช้จริง 1-5 ชิ้น)")]
        public GameObject slotRoot;

        [Tooltip("Image แสดงรูปไอคอนวัตถุดิบ")]
        public Image iconImage;

        [Tooltip("TMP_Text แสดงจำนวนที่ใช้ เช่น 5, 50, 10, 1")]
        public TMP_Text amountText;

        [Tooltip("Image กรอบหรือพื้นหลังของช่องวัตถุดิบ (Optional)")]
        public Image frameImage;

        public void SetIngredient(Sprite icon, int requiredAmount, bool hasEnough, Color enoughTextColor, Color missingTextColor)
        {
            if (slotRoot != null)
            {
                slotRoot.SetActive(true);
            }

            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }

            if (amountText != null)
            {
                amountText.text = requiredAmount.ToString();
                amountText.color = hasEnough ? enoughTextColor : missingTextColor;
            }
        }

        public void Hide()
        {
            if (slotRoot != null)
            {
                slotRoot.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Component สำหรับแถวรายการคราฟต์ 1 แถว (CraftThumbnail)
    /// แสดงผล: ไอคอนผลลัพธ์ + ชื่อไอเทม + ช่องวัตถุดิบสูงสุด 5 ช่อง + ปุ่มกดคราฟต์ (เปลี่ยนสีดำ/เหลืองตามสถานะคราฟต์ได้)
    /// </summary>
    public class CraftThumbnail : MonoBehaviour
    {
        [Header("Result Item UI (ฝั่งซ้าย)")]
        [Tooltip("Image แสดงไอคอนของไอเทมที่จะได้จากการคราฟต์")]
        [SerializeField] private Image resultIconImage;

        [Tooltip("TMP_Text แสดงชื่อไอเทมที่จะได้จากการคราฟต์")]
        [SerializeField] private TMP_Text resultNameText;

        [Tooltip("TMP_Text แสดงจำนวนผลลัพธ์ที่ได้ (Optional ถ้าไม่ใส่จะต่อท้ายชื่อเมื่อ > 1)")]
        [SerializeField] private TMP_Text resultAmountText;

        [Header("Ingredient Slots (สูงสุด 5 ช่องตรงกลาง)")]
        [Tooltip("รายการช่องวัตถุดิบทั้ง 5 ช่อง (หากไม่ลากใส่ สามารถกด Context Menu -> Auto Bind Child Components ได้)")]
        [SerializeField] private List<CraftIngredientSlotUI> ingredientSlots = new List<CraftIngredientSlotUI>(CraftingRecipe.MaxIngredients);

        [Tooltip("Transform พ่อของช่องวัตถุดิบทั้ง 5 ช่อง (ใช้หาลูกอัตโนมัติถ้ายังไม่ได้ผูก ingredientSlots)")]
        [SerializeField] private Transform ingredientsContainer;

        [Header("Craft Button UI (ปุ่มรูปประแจฝั่งขวา)")]
        [Tooltip("Button สำหรับกดคราฟต์ไอเทม")]
        [SerializeField] private Button craftButton;

        [Tooltip("Image พื้นหลังของปุ่มคราฟต์ (ใช้เปลี่ยนสีเหลืองเมื่อคราฟต์ได้ / สีดำเมื่อของไม่พอ)")]
        [SerializeField] private Image craftButtonBackground;

        [Tooltip("Image ไอคอนรูปประแจบนปุ่มคราฟต์ (Optional)")]
        [SerializeField] private Image craftButtonIcon;

        [Header("Visual State Settings (สีปุ่ม & สีตัวเลขตามรูป UI)")]
        [Tooltip("Sprite ปุ่มเมื่อคราฟต์ได้ (สีเหลือง) - Optional ถ้าไม่ใส่จะใช้การเปลี่ยนสี Color แทน")]
        [SerializeField] private Sprite canCraftButtonSprite;

        [Tooltip("Sprite ปุ่มเมื่อคราฟต์ไม่ได้ (สีดำ/เทา) - Optional")]
        [SerializeField] private Sprite cannotCraftButtonSprite;

        [Tooltip("สีปุ่มเมื่อมีของครบสามารถคราฟต์ได้ (ค่าเริ่มต้น: สีเหลืองทองตามรูป)")]
        [SerializeField] private Color canCraftButtonColor = new Color(1f, 0.78f, 0.05f, 1f);

        [Tooltip("สีปุ่มเมื่อของไม่พอคราฟต์ไม่ได้ (ค่าเริ่มต้น: สีดำเทาเข้มตามรูป)")]
        [SerializeField] private Color cannotCraftButtonColor = new Color(0.14f, 0.14f, 0.14f, 1f);

        [Tooltip("สีตัวเลขจำนวนวัตถุดิบเมื่อมีของพอ")]
        [SerializeField] private Color enoughIngredientTextColor = Color.white;

        [Tooltip("สีตัวเลขจำนวนวัตถุดิบเมื่อของในกระเป๋าไม่พอ")]
        [SerializeField] private Color missingIngredientTextColor = new Color(1f, 0.35f, 0.35f, 1f);

        private CraftingRecipe _currentRecipe;
        private Action<CraftingRecipe> _onClickCraft;

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
        /// ดึง Component ลูกอัตโนมัติด้วย GetComponent / GetComponentsInChildren หากยังไม่ได้ลากใส่ใน Inspector
        /// </summary>
        [ContextMenu("Auto Bind Child Components")]
        public void AutoBindIfNeeded()
        {
            if (craftButton == null)
            {
                craftButton = GetComponentInChildren<Button>(true);
            }

            if (craftButton != null && craftButtonBackground == null)
            {
                craftButtonBackground = craftButton.GetComponent<Image>();
            }

            if (resultNameText == null)
            {
                TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
                if (texts.Length > 0)
                {
                    resultNameText = texts[0];
                }
            }

            if (resultIconImage == null)
            {
                Image[] images = GetComponentsInChildren<Image>(true);
                foreach (Image img in images)
                {
                    if (img.gameObject != gameObject &&
                        (craftButton == null || !img.transform.IsChildOf(craftButton.transform)) &&
                        (ingredientsContainer == null || !img.transform.IsChildOf(ingredientsContainer)))
                    {
                        resultIconImage = img;
                        break;
                    }
                }
            }

            // Auto-bind ช่องวัตถุดิบสูงสุด 5 ช่องจาก ingredientsContainer (ถ้ามีการระบุ Container ไว้)
            if ((ingredientSlots == null || ingredientSlots.Count == 0) && ingredientsContainer != null)
            {
                ingredientSlots = new List<CraftIngredientSlotUI>();
                int childCount = Mathf.Min(ingredientsContainer.childCount, CraftingRecipe.MaxIngredients);
                for (int i = 0; i < childCount; i++)
                {
                    Transform slotTrans = ingredientsContainer.GetChild(i);
                    CraftIngredientSlotUI slotUI = new CraftIngredientSlotUI
                    {
                        slotRoot = slotTrans.gameObject,
                        frameImage = slotTrans.GetComponent<Image>(),
                        amountText = slotTrans.GetComponentInChildren<TMP_Text>(true)
                    };

                    Image[] childImgs = slotTrans.GetComponentsInChildren<Image>(true);
                    foreach (Image img in childImgs)
                    {
                        if (img.gameObject != slotTrans.gameObject)
                        {
                            slotUI.iconImage = img;
                            break;
                        }
                    }
                    if (slotUI.iconImage == null)
                    {
                        slotUI.iconImage = slotUI.frameImage;
                    }

                    ingredientSlots.Add(slotUI);
                }
            }
        }

        /// <summary>
        /// เซตข้อมูลสูตรคราฟต์ลงใน Thumbnail แถวนี้
        /// </summary>
        public void SetData(CraftingRecipe recipe, PlayerInventory playerInventory, Action<CraftingRecipe> onClickCraft)
        {
            AutoBindIfNeeded();

            _currentRecipe = recipe;
            _onClickCraft = onClickCraft;

            if (recipe == null)
            {
                Clear();
                return;
            }

            // 1. ตั้งค่าไอคอนผลลัพธ์
            Sprite resultIcon = recipe.ResolvedResultIcon;
            if (resultIconImage != null)
            {
                resultIconImage.sprite = resultIcon;
                resultIconImage.enabled = resultIcon != null;
            }

            // 2. ตั้งค่าชื่อไอเทมผลลัพธ์ และจำนวนที่ได้
            string displayName = !string.IsNullOrEmpty(recipe.recipeName)
                ? recipe.recipeName
                : recipe.ResolvedResultName;

            if (resultNameText != null)
            {
                if (resultAmountText == null && recipe.resultAmount > 1)
                {
                    resultNameText.text = $"{displayName} x{recipe.resultAmount}";
                }
                else
                {
                    resultNameText.text = displayName;
                }
            }

            if (resultAmountText != null)
            {
                resultAmountText.text = recipe.resultAmount > 1 ? $"x{recipe.resultAmount}" : string.Empty;
                resultAmountText.gameObject.SetActive(recipe.resultAmount > 1);
            }

            // 3. ตั้งค่าช่องวัตถุดิบ (1 ถึง 5 ช่อง) และปุ่มกดคราฟต์
            RefreshState(playerInventory);

            if (craftButton != null)
            {
                craftButton.onClick.RemoveAllListeners();
                craftButton.onClick.AddListener(HandleCraftButtonClicked);
            }
        }

        /// <summary>
        /// อัปเดตสถานะจำนวนวัตถุดิบและสีปุ่มคราฟต์ตามของที่มีใน PlayerInventory ล่าสุด
        /// </summary>
        public void RefreshState(PlayerInventory playerInventory)
        {
            if (_currentRecipe == null) return;

            List<CraftingIngredient> ingredients = _currentRecipe.ingredients;
            int requiredCount = ingredients != null ? Mathf.Min(ingredients.Count, CraftingRecipe.MaxIngredients) : 0;
            bool canCraftAll = requiredCount > 0;

            for (int i = 0; i < ingredientSlots.Count; i++)
            {
                CraftIngredientSlotUI slotUI = ingredientSlots[i];
                if (slotUI == null) continue;

                if (i < requiredCount)
                {
                    CraftingIngredient ing = ingredients[i];
                    string ingName = ing.ResolvedItemName;
                    Sprite ingIcon = ing.ResolvedIcon;
                    int ownedAmount = playerInventory != null ? playerInventory.GetItemCount(ingName) : 0;
                    bool hasEnough = ownedAmount >= ing.amount;

                    if (!hasEnough)
                    {
                        canCraftAll = false;
                    }

                    slotUI.SetIngredient(
                        ingIcon,
                        ing.amount,
                        hasEnough,
                        enoughIngredientTextColor,
                        missingIngredientTextColor
                    );
                }
                else
                {
                    // ซ่อนช่องที่ไม่ได้ใช้ (เช่น ใช้แค่ 2 หรือ 3 อย่าง ช่องที่เหลือจะถูกปิด)
                    slotUI.Hide();
                }
            }

            // หากยังไม่ได้ลาก ingredientSlots แต่เช็กจาก CraftingDatabase โดยตรง
            if (ingredientSlots.Count == 0 && CraftingDatabase.Instance != null)
            {
                canCraftAll = CraftingDatabase.Instance.CanCraft(_currentRecipe, playerInventory);
            }

            // อัปเดตสถานะปุ่มคราฟต์ (สีเหลืองเมื่อคราฟต์ได้ / สีดำเทาเมื่อคราฟต์ไม่ได้)
            if (craftButton != null)
            {
                craftButton.interactable = canCraftAll;
            }

            if (craftButtonBackground != null)
            {
                if (canCraftAll && canCraftButtonSprite != null)
                {
                    craftButtonBackground.sprite = canCraftButtonSprite;
                    craftButtonBackground.color = Color.white;
                }
                else if (!canCraftAll && cannotCraftButtonSprite != null)
                {
                    craftButtonBackground.sprite = cannotCraftButtonSprite;
                    craftButtonBackground.color = Color.white;
                }
                else
                {
                    craftButtonBackground.color = canCraftAll ? canCraftButtonColor : cannotCraftButtonColor;
                }
            }
        }

        /// <summary>
        /// ล้างข้อมูลในแถวเมื่อไม่ได้ใช้งาน
        /// </summary>
        public void Clear()
        {
            _currentRecipe = null;
            _onClickCraft = null;

            if (resultIconImage != null)
            {
                resultIconImage.sprite = null;
                resultIconImage.enabled = false;
            }

            if (resultNameText != null)
            {
                resultNameText.text = string.Empty;
            }

            if (resultAmountText != null)
            {
                resultAmountText.text = string.Empty;
            }

            if (ingredientSlots != null)
            {
                for (int i = 0; i < ingredientSlots.Count; i++)
                {
                    ingredientSlots[i]?.Hide();
                }
            }

            if (craftButton != null)
            {
                craftButton.onClick.RemoveAllListeners();
                craftButton.interactable = false;
            }
        }

        private void HandleCraftButtonClicked()
        {
            if (_currentRecipe != null)
            {
                _onClickCraft?.Invoke(_currentRecipe);
            }
        }
    }
}

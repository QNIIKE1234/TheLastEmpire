using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// หมวดหมู่การคราฟต์ตามหน้าต่าง UI (Building, Utility, Defense, Weapons, Consumables, Tools, Special)
    /// </summary>
    public enum CraftingCategory
    {
        Building,
        Utility,
        Defense,
        Weapons,
        Consumables,
        Tools,
        Special
    }

    /// <summary>
    /// ข้อมูลวัตถุดิบแต่ละชิ้นที่ใช้ในการคราฟต์ (สูงสุดไม่เกิน 5 ชนิดต่อ 1 สูตร)
    /// </summary>
    [Serializable]
    public struct CraftingIngredient
    {
        [Tooltip("อ้างอิง ItemData ของวัตถุดิบจาก ItemDatabase")]
        public ItemData item;

        [Tooltip("ชื่อไอเทมวัตถุดิบ (ซิงก์อัตโนมัติจาก ItemData หรือระบุชื่อโดยตรง)")]
        public string itemName;

        [Tooltip("จำนวนที่ต้องใช้")]
        [Min(1)]
        public int amount;

        public CraftingIngredient(ItemData item, string itemName, int amount)
        {
            this.item = item;
            this.itemName = item != null && !string.IsNullOrEmpty(item.itemName) ? item.itemName : itemName;
            this.amount = Mathf.Max(1, amount);
        }

        public string ResolvedItemName =>
            item != null && !string.IsNullOrEmpty(item.itemName) ? item.itemName : itemName;

        public Sprite ResolvedIcon =>
            item != null ? item.icon : (ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItemByName(itemName)?.icon : null);
    }

    /// <summary>
    /// สูตรการคราฟต์ 1 รายการ (ใช้วัตถุดิบสูงสุด 5 อย่าง -> ได้ผลลัพธ์ 1 อย่างพร้อมจำนวน)
    /// </summary>
    [Serializable]
    public class CraftingRecipe
    {
        public const int MaxIngredients = 5;

        [Header("Recipe Identity")]
        [Tooltip("รหัสสูตรคราฟต์ เช่น CRAFT0001")]
        public string recipeId;

        [Tooltip("ชื่อที่แสดงในหน้า UI คราฟต์")]
        public string recipeName;

        [Tooltip("หมวดหมู่ของไอเทมคราฟต์")]
        public CraftingCategory category;

        [Header("Craft Result (ของที่ได้จากการคราฟต์)")]
        [Tooltip("ไอเทมผลลัพธ์ที่ได้จากการคราฟต์")]
        public ItemData resultItem;

        [Tooltip("ชื่อไอเทมผลลัพธ์ (ซิงก์อัตโนมัติจาก resultItem หรือใช้ระบุชื่อสิ่งปลูกสร้าง)")]
        public string resultItemName;

        [Tooltip("จำนวนผลลัพธ์ที่ได้ต่อการคราฟต์ 1 ครั้ง")]
        [Min(1)]
        public int resultAmount = 1;

        [Tooltip("รูปไอคอนของผลลัพธ์ (Optional หากต้องการเจาะจงรูปไอคอน เช่น สิ่งปลูกสร้าง หรือไอคอนเฉพาะ)")]
        public Sprite icon;

        [TextArea(1, 3)]
        [Tooltip("คำอธิบายสูตรคราฟต์")]
        public string description;

        [Header("Required Ingredients (สูงสุด 5 อย่าง)")]
        [Tooltip("รายการวัตถุดิบที่ต้องใช้ (จำกัดไม่เกิน 5 ชนิด)")]
        public List<CraftingIngredient> ingredients = new List<CraftingIngredient>();

        public string ResolvedResultName =>
            !string.IsNullOrEmpty(resultItemName)
                ? resultItemName
                : (resultItem != null && !string.IsNullOrEmpty(resultItem.itemName) ? resultItem.itemName : recipeName);

        public Sprite ResolvedResultIcon
        {
            get
            {
                if (icon != null)
                {
                    return icon;
                }

                if (category == CraftingCategory.Building || category == CraftingCategory.Utility || category == CraftingCategory.Defense)
                {
                    if (BuildingManager.Instance != null)
                    {
                        BuildingData bData = BuildingManager.Instance.GetBuildingDataForRecipe(this);
                        if (bData != null && bData.icon != null)
                        {
                            return bData.icon;
                        }
                    }
                }

                if (resultItem != null && resultItem.icon != null)
                {
                    return resultItem.icon;
                }

                if (ItemDatabase.Instance != null)
                {
                    Sprite itemIcon = ItemDatabase.Instance.GetItemByName(ResolvedResultName)?.icon;
                    if (itemIcon != null) return itemIcon;
                }

                if (BuildingManager.Instance != null)
                {
                    BuildingData bData = BuildingManager.Instance.GetBuildingDataForRecipe(this);
                    if (bData != null && bData.icon != null)
                    {
                        return bData.icon;
                    }
                }

                return null;
            }
        }

        public void Validate()
        {
            if (resultAmount < 1)
            {
                resultAmount = 1;
            }

            if (resultItem != null && !string.IsNullOrEmpty(resultItem.itemName))
            {
                if (string.IsNullOrEmpty(resultItemName))
                {
                    resultItemName = resultItem.itemName;
                }
                if (string.IsNullOrEmpty(recipeName))
                {
                    recipeName = resultItemName;
                }
            }

            if (ingredients == null)
            {
                ingredients = new List<CraftingIngredient>();
                return;
            }

            if (ingredients.Count > MaxIngredients)
            {
                ingredients.RemoveRange(MaxIngredients, ingredients.Count - MaxIngredients);
            }

            for (int i = 0; i < ingredients.Count; i++)
            {
                CraftingIngredient ing = ingredients[i];
                if (ing.amount < 1)
                {
                    ing.amount = 1;
                }
                if (ing.item != null && !string.IsNullOrEmpty(ing.item.itemName))
                {
                    ing.itemName = ing.item.itemName;
                }
                ingredients[i] = ing;
            }
        }
    }

    /// <summary>
    /// ScriptableObject Database สำหรับเก็บสูตรคราฟต์ทั้งหมดของเกม
    /// </summary>
    [CreateAssetMenu(fileName = "CraftingDatabase", menuName = "TheLastEmpire/Crafting Database")]
    public class CraftingDatabase : ScriptableObject
    {
        private static CraftingDatabase _instance;
        public static CraftingDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = AddressableManager.LoadAsset<CraftingDatabase>("Database/CraftingDatabase");
                    if (_instance == null)
                    {
                        _instance = AddressableManager.LoadAsset<CraftingDatabase>("CraftingDatabase");
                    }
                    if (_instance == null)
                    {
                        CraftingDatabase[] databases = Resources.LoadAll<CraftingDatabase>("");
                        if (databases.Length > 0)
                        {
                            _instance = databases[0];
                        }
                    }
                    if (_instance == null)
                    {
                        CraftingDatabase[] databases = Resources.FindObjectsOfTypeAll<CraftingDatabase>();
                        if (databases.Length > 0)
                        {
                            _instance = databases[0];
                        }
                    }
                }
                return _instance;
            }
        }

        [Header("All Crafting Recipes")]
        public List<CraftingRecipe> recipes = new List<CraftingRecipe>();

        /// <summary>
        /// โหมด Free Craft: เมื่อเปิดเป็น true จะสามารถคราฟต์ได้ทุกสูตรโดยไม่ต้องมีวัตถุดิบ และไม่หักของออกจากกระเป๋า
        /// </summary>
        public static bool IsFreeCraft { get; set; } = false;

        private void OnValidate()
        {
            if (recipes == null) return;
            for (int i = 0; i < recipes.Count; i++)
            {
                recipes[i]?.Validate();
            }
        }

        /// <summary>
        /// ดึงสูตรคราฟต์ตาม Recipe ID เช่น "CRAFT0001"
        /// </summary>
        public CraftingRecipe GetRecipeById(string id)
        {
            if (recipes == null || string.IsNullOrEmpty(id)) return null;
            return recipes.Find(r => r != null && string.Equals(r.recipeId, id, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// ดึงสูตรคราฟต์ตามชื่อ เช่น "Wooden Lantern Post"
        /// </summary>
        public CraftingRecipe GetRecipeByName(string name)
        {
            if (recipes == null || string.IsNullOrEmpty(name)) return null;
            return recipes.Find(r => r != null && (string.Equals(r.recipeName, name, StringComparison.OrdinalIgnoreCase) || string.Equals(r.ResolvedResultName, name, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        /// ดึงรายการสูตรคราฟต์ตามหมวดหมู่ (เช่น Building, Utility, Defense, Weapons, Consumables, Tools, Special)
        /// </summary>
        public List<CraftingRecipe> GetRecipesByCategory(CraftingCategory category)
        {
            List<CraftingRecipe> result = new List<CraftingRecipe>();
            if (recipes == null) return result;

            foreach (CraftingRecipe recipe in recipes)
            {
                if (recipe != null && recipe.category == category)
                {
                    result.Add(recipe);
                }
            }
            return result;
        }

        /// <summary>
        /// ค้นหาสูตรคราฟต์ด้วยคำค้นหา (Search Bar) และกรองตามหมวดหมู่ได้
        /// </summary>
        public List<CraftingRecipe> SearchRecipes(string searchQuery, CraftingCategory? categoryFilter = null)
        {
            List<CraftingRecipe> result = new List<CraftingRecipe>();
            if (recipes == null) return result;

            string query = string.IsNullOrWhiteSpace(searchQuery) ? string.Empty : searchQuery.Trim();

            foreach (CraftingRecipe recipe in recipes)
            {
                if (recipe == null) continue;
                if (categoryFilter.HasValue && recipe.category != categoryFilter.Value) continue;

                if (string.IsNullOrEmpty(query) ||
                    (!string.IsNullOrEmpty(recipe.recipeName) && recipe.recipeName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(recipe.ResolvedResultName) && recipe.ResolvedResultName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    result.Add(recipe);
                }
            }
            return result;
        }

        /// <summary>
        /// ตรวจสอบว่าผู้เล่นมีวัตถุดิบในกระเป๋าครบตามสูตรหรือไม่ (หากเปิด IsFreeCraft จะเป็น true เสมอ)
        /// </summary>
        public bool CanCraft(CraftingRecipe recipe, PlayerInventory inventory)
        {
            if (IsFreeCraft) return true;

            if (recipe == null || inventory == null || recipe.ingredients == null || recipe.ingredients.Count == 0)
                return false;

            foreach (CraftingIngredient ing in recipe.ingredients)
            {
                string reqName = ing.ResolvedItemName;
                if (string.IsNullOrEmpty(reqName)) return false;

                if (inventory.GetItemCount(reqName) < ing.amount)
                {
                    return false;
                }
            }
            return true;
        }
    }
}

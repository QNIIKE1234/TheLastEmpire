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
        Special,
        Artifact
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

        [NonSerialized]
        private List<CraftingRecipe> _runtimeArtifactRecipes;

        [NonSerialized]
        private List<CraftingRecipe> _runtimeFurnitureRecipes;

        [NonSerialized]
        private bool _cachedArtifactRecipesIncludedLegendary = false;

        /// <summary>
        /// โหมด Free Craft: เมื่อเปิดเป็น true จะสามารถคราฟต์ได้ทุกสูตรโดยไม่ต้องมีวัตถุดิบ และไม่หักของออกจากกระเป๋า
        /// </summary>
        public static bool IsFreeCraft { get; set; } = false;

        /// <summary>
        /// โหมด Cheat: เมื่อเปิดเป็น true จะปลดล็อกให้โชว์และคราฟต์ Artifact ระดับ Legendary ทั้งหมดในแท็บ Artifact
        /// </summary>
        public static bool UnlockLegendaryArtifactCraft { get; set; } = false;

        /// <summary>
        /// สร้างรายการสูตรคราฟต์สำหรับ Artifact ทั้งหมดจาก ItemDatabase อัตโนมัติ
        /// - รวมทุกชิ้นของทุกอาวุธ + ชิ้นที่ใช้ร่วมกัน (Universal/Shared) แสดงแค่ 1 รายการไม่ซ้ำ
        /// - โหมดปกติแสดงเฉพาะ Common และ Rare (Legendary ดรอปจากบอสเท่านั้น)
        /// - เมื่อเปิด Cheat UnlockLegendaryArtifactCraft จะปลดล็อกแสดง Legendary ทุกชิ้นขึ้นหน้าแรกทันที
        /// </summary>
        private List<CraftingRecipe> GetRuntimeArtifactRecipes()
        {
            bool includeLegendary = UnlockLegendaryArtifactCraft;
            if (_runtimeArtifactRecipes != null &&
                _runtimeArtifactRecipes.Count > 0 &&
                _cachedArtifactRecipesIncludedLegendary == includeLegendary)
            {
                return _runtimeArtifactRecipes;
            }

            _cachedArtifactRecipesIncludedLegendary = includeLegendary;
            _runtimeArtifactRecipes = new List<CraftingRecipe>();
            if (ItemDatabase.Instance == null || ItemDatabase.Instance.allItems == null)
            {
                return _runtimeArtifactRecipes;
            }

            // ดึง ItemData วัตถุดิบจริงเพื่อให้แสดงไอคอนวัตถุดิบใน UI คราฟต์ได้ทันที
            ItemData scrapPlate = ItemDatabase.Instance.GetItemByName("Scrap Metal Plate");
            ItemData logicChip = ItemDatabase.Instance.GetItemByName("Basic Logic Chip");
            ItemData charcoal = ItemDatabase.Instance.GetItemByName("Charcoal Lump");
            ItemData titaniumPlate = ItemDatabase.Instance.GetItemByName("Reinforced Titanium Plate");
            ItemData capacitor = ItemDatabase.Instance.GetItemByName("High-Capacity Capacitor");
            ItemData chitinShard = ItemDatabase.Instance.GetItemByName("Chitin Carapace Shard");

            var seenArtifactNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var artifactItems = new List<ItemData>();

            for (int i = 0; i < ItemDatabase.Instance.allItems.Count; i++)
            {
                ItemData item = ItemDatabase.Instance.allItems[i];
                if (item == null || item.type != ItemType.Artifact || item.artifactConfig == null) continue;
                if (string.IsNullOrWhiteSpace(item.itemName)) continue;

                // โหมดปกติ: คราฟต์ได้เฉพาะ Common และ Rare (Legendary ดรอปจากบอสเท่านั้น เว้นแต่เปิด Cheat)
                if (!includeLegendary && item.artifactConfig.rarity == ArtifactRarity.Legendary) continue;

                // ชิ้นที่ใช้ร่วมกันหรือชื่อเดียวกันแสดงคราฟต์แค่ 1 รายการเดียว
                if (!seenArtifactNames.Add(item.itemName.Trim())) continue;

                artifactItems.Add(item);
            }

            // เรียงลำดับ: หากเปิด Cheat ปลดล็อก Legendary ให้เอา Legendary ขึ้นหน้าแรกก่อน จากนั้น Common -> Rare
            artifactItems.Sort((a, b) =>
            {
                if (includeLegendary)
                {
                    bool aLeg = a.artifactConfig.rarity == ArtifactRarity.Legendary;
                    bool bLeg = b.artifactConfig.rarity == ArtifactRarity.Legendary;
                    if (aLeg != bLeg) return aLeg ? -1 : 1;
                }

                int rCmp = ((int)a.artifactConfig.rarity).CompareTo((int)b.artifactConfig.rarity);
                if (rCmp != 0) return rCmp;

                bool aShared = a.artifactConfig.compatibleWeaponNames == null || a.artifactConfig.compatibleWeaponNames.Count != 1;
                bool bShared = b.artifactConfig.compatibleWeaponNames == null || b.artifactConfig.compatibleWeaponNames.Count != 1;
                if (aShared != bShared) return aShared ? -1 : 1;

                string aWep = aShared ? "" : (a.artifactConfig.compatibleWeaponNames[0] ?? "");
                string bWep = bShared ? "" : (b.artifactConfig.compatibleWeaponNames[0] ?? "");
                int wCmp = string.Compare(aWep, bWep, StringComparison.OrdinalIgnoreCase);
                if (wCmp != 0) return wCmp;

                return string.Compare(a.itemName, b.itemName, StringComparison.OrdinalIgnoreCase);
            });

            for (int i = 0; i < artifactItems.Count; i++)
            {
                ItemData item = artifactItems[i];
                ArtifactModConfig cfg = item.artifactConfig;
                bool isLegendary = cfg.rarity == ArtifactRarity.Legendary;
                bool isRare = cfg.rarity == ArtifactRarity.Rare;
                string rarityTag = isLegendary ? "Legendary" : (isRare ? "Rare" : "Common");

                string weaponLabel;
                if (cfg.compatibleWeaponNames == null || cfg.compatibleWeaponNames.Count == 0)
                {
                    weaponLabel = "All Weapons";
                }
                else if (cfg.compatibleWeaponNames.Count == 1)
                {
                    weaponLabel = cfg.compatibleWeaponNames[0];
                }
                else
                {
                    weaponLabel = "All Guns";
                }

                string effectBody = !string.IsNullOrWhiteSpace(item.description)
                    ? item.description.Trim()
                    : WeaponArtifactManager.BuildSingleArtifactSummary(item);
                string desc = $"{rarityTag} Artifact\nWeapon: {weaponLabel}\n{effectBody}";

                var recipe = new CraftingRecipe
                {
                    recipeId = $"CRAFT_ART_{(i + 1):D3}",
                    recipeName = item.itemName,
                    category = CraftingCategory.Artifact,
                    resultItem = item,
                    resultItemName = item.itemName,
                    resultAmount = 1,
                    icon = item.icon,
                    description = desc,
                    ingredients = isLegendary
                        ? new List<CraftingIngredient>
                        {
                            new CraftingIngredient(titaniumPlate, "Reinforced Titanium Plate", 5),
                            new CraftingIngredient(capacitor, "High-Capacity Capacitor", 5),
                            new CraftingIngredient(chitinShard, "Chitin Carapace Shard", 5)
                        }
                        : isRare
                            ? new List<CraftingIngredient>
                            {
                                new CraftingIngredient(titaniumPlate, "Reinforced Titanium Plate", 2),
                                new CraftingIngredient(capacitor, "High-Capacity Capacitor", 2),
                                new CraftingIngredient(chitinShard, "Chitin Carapace Shard", 2)
                            }
                            : new List<CraftingIngredient>
                            {
                                new CraftingIngredient(scrapPlate, "Scrap Metal Plate", 3),
                                new CraftingIngredient(logicChip, "Basic Logic Chip", 1),
                                new CraftingIngredient(charcoal, "Charcoal Lump", 2)
                            }
                };

                _runtimeArtifactRecipes.Add(recipe);
            }

            return _runtimeArtifactRecipes;
        }

        /// <summary>
        /// Guarantees all 30 Furniture recipes (BUILD0024..BUILD0053) appear in the Utility tab even if
        /// Unity loaded an older cached Addressable CraftingDatabase asset. Deduplicates by recipeId/name.
        /// </summary>
        private bool _furnitureIconsResolved = false;

        private List<CraftingRecipe> GetRuntimeFurnitureRecipes()
        {
            if (_runtimeFurnitureRecipes != null)
            {
                // Lazy-refresh icons once BuildingManager finishes loading BuildingData assets
                if (!_furnitureIconsResolved && BuildingManager.Instance != null)
                {
                    int resolved = 0;
                    for (int i = 0; i < _runtimeFurnitureRecipes.Count; i++)
                    {
                        CraftingRecipe r = _runtimeFurnitureRecipes[i];
                        if (r == null) continue;
                        BuildingData bData = BuildingManager.Instance.GetBuildingDataByName(r.recipeName);
                        if (bData != null && bData.icon != null)
                        {
                            r.icon = bData.icon;
                            resolved++;
                        }
                    }
                    if (resolved > 0) _furnitureIconsResolved = true;
                }
                return _runtimeFurnitureRecipes;
            }

            _runtimeFurnitureRecipes = new List<CraftingRecipe>();
            var existingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (recipes != null)
            {
                for (int i = 0; i < recipes.Count; i++)
                {
                    if (recipes[i] == null) continue;
                    if (!string.IsNullOrEmpty(recipes[i].recipeId)) existingIds.Add(recipes[i].recipeId);
                    if (!string.IsNullOrEmpty(recipes[i].recipeName)) existingNames.Add(recipes[i].recipeName);
                }
            }

            // Pick clean fallback icons from existing Utility recipes in this database
            Sprite iconCrate = recipes?.Find(r => r != null && r.recipeId == "CRAFT0011")?.icon;
            Sprite iconBench = recipes?.Find(r => r != null && r.recipeId == "CRAFT0010")?.icon ?? iconCrate;
            Sprite iconBed   = recipes?.Find(r => r != null && r.recipeId == "CRAFT0013")?.icon ?? iconCrate;
            Sprite iconLamp  = recipes?.Find(r => r != null && r.recipeId == "CRAFT0014")?.icon ?? iconCrate;
            Sprite iconPot   = recipes?.Find(r => r != null && r.recipeId == "CRAFT0009")?.icon ?? iconCrate;
            Sprite iconUtil  = recipes?.Find(r => r != null && r.recipeId == "CRAFT0012")?.icon ?? iconCrate;

            ItemData GetItem(string n) => ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItemByName(n) : null;
            CraftingIngredient Ing(string n, int amt) => new CraftingIngredient(GetItem(n), n, amt);

            int initialResolved = 0;
            void AddFurn(string cid, string rName, Sprite fallbackIc, string desc, params CraftingIngredient[] ings)
            {
                if (existingIds.Contains(cid) || existingNames.Contains(rName)) return;
                Sprite realIcon = fallbackIc;
                if (BuildingManager.Instance != null)
                {
                    BuildingData bData = BuildingManager.Instance.GetBuildingDataByName(rName);
                    if (bData != null && bData.icon != null)
                    {
                        realIcon = bData.icon;
                        initialResolved++;
                    }
                }
                _runtimeFurnitureRecipes.Add(new CraftingRecipe
                {
                    recipeId = cid,
                    recipeName = rName,
                    category = CraftingCategory.Utility,
                    resultItem = null,
                    resultItemName = rName,
                    resultAmount = 1,
                    icon = realIcon,
                    description = desc,
                    ingredients = new List<CraftingIngredient>(ings)
                });
            }

            AddFurn("CRAFT0077", "Wooden Wardrobe",        iconCrate, "Tall double-door wooden wardrobe for storing gear (24 slots).", Ing("Wood Log", 8), Ing("Box of Nails", 4), Ing("Industrial Glue", 2));
            AddFurn("CRAFT0078", "Metal Supply Cabinet",   iconCrate, "Heavy industrial steel locker cabinet (30 slots).", Ing("Scrap Metal Plate", 6), Ing("Scrap Steel Pipe", 3), Ing("Box of Nails", 4));
            AddFurn("CRAFT0079", "Carpentry Craft Table",  iconBench, "Sturdy wooden carpentry table. Press [E] to craft.", Ing("Wood Log", 8), Ing("Scrap Metal Plate", 3), Ing("Box of Nails", 4), Ing("Duct Tape", 2));
            AddFurn("CRAFT0080", "Engineer Craft Desk",    iconBench, "Technical craft desk with monitor. Press [E] to craft.", Ing("Scrap Metal Plate", 5), Ing("Basic Logic Chip", 2), Ing("Copper Wire Spool", 3), Ing("Scrap Steel Pipe", 2));
            AddFurn("CRAFT0081", "Wooden Chair",           iconBed,   "Simple wooden chair. Press [E] to sit and restore Sanity.", Ing("Wood Log", 4), Ing("Box of Nails", 2));
            AddFurn("CRAFT0082", "Lounge Armchair",        iconBed,   "Plush lounge armchair. Press [E] to relax and restore Sanity.", Ing("Wood Log", 5), Ing("Torn Infected Cloth", 4), Ing("Scrap Leather", 3), Ing("Box of Nails", 2));
            AddFurn("CRAFT0083", "Wooden Bed",             iconBed,   "Full wooden bed. Press [E] to set Respawn, [Z] to sleep at night.", Ing("Wood Log", 8), Ing("Torn Infected Cloth", 6), Ing("Scrap Leather", 4), Ing("Plant Fiber", 6));
            AddFurn("CRAFT0084", "Analog Radio",           iconLamp,  "Retro analog FM radio. Press [E] Power, [Z] switch 3 music stations.", Ing("Wood Log", 3), Ing("Copper Wire Spool", 3), Ing("Basic Logic Chip", 1), Ing("Scrap Metal Plate", 2));
            AddFurn("CRAFT0085", "Table Desk Lamp",        iconLamp,  "Warm bedside desk lamp. Press [E] to toggle ON/OFF.", Ing("Wood Log", 3), Ing("Clean Glass Bottle", 2), Ing("Copper Wire Spool", 2));
            AddFurn("CRAFT0086", "Cooking Pot",            iconPot,   "Cast-iron cooking pot on a stove burner.", Ing("Scrap Metal Plate", 5), Ing("Scrap Steel Pipe", 2), Ing("Charcoal Lump", 3));
            AddFurn("CRAFT0087", "Lime Planter Box",       iconUtil,  "Square planter box growing Fresh Limes. Press [E] to harvest.", Ing("Wood Log", 6), Ing("Plant Fiber", 6), Ing("Box of Nails", 2));
            AddFurn("CRAFT0088", "Potato Planter Box",     iconUtil,  "Square planter box growing Potatoes. Press [E] to harvest.", Ing("Wood Log", 6), Ing("Plant Fiber", 6), Ing("Box of Nails", 2));
            AddFurn("CRAFT0089", "Tomato Planter Box",     iconUtil,  "Square planter box growing Ripe Tomatoes. Press [E] to harvest.", Ing("Wood Log", 6), Ing("Plant Fiber", 6), Ing("Box of Nails", 2));
            AddFurn("CRAFT0090", "Carrot Planter Box",     iconUtil,  "Square planter box growing Sweet Carrots. Press [E] to harvest.", Ing("Wood Log", 6), Ing("Plant Fiber", 6), Ing("Box of Nails", 2));
            AddFurn("CRAFT0091", "Strawberry Planter Box", iconUtil,  "Square planter box growing Strawberries. Press [E] to harvest.", Ing("Wood Log", 6), Ing("Plant Fiber", 6), Ing("Box of Nails", 2));
            AddFurn("CRAFT0092", "Refrigerator",           iconCrate, "Vintage kitchen refrigerator (24 slots).", Ing("Scrap Metal Plate", 6), Ing("Copper Wire Spool", 2), Ing("Scrap Steel Pipe", 2));
            AddFurn("CRAFT0093", "Medical Cabinet",        iconCrate, "Clinical medical supply cabinet (18 slots).", Ing("Scrap Metal Plate", 4), Ing("Clean Glass Bottle", 2), Ing("Duct Tape", 2));
            AddFurn("CRAFT0094", "Leather Sofa",           iconBed,   "Wide leather sofa. Press [E] to sit and recover Sanity.", Ing("Wood Log", 6), Ing("Scrap Leather", 6), Ing("Torn Infected Cloth", 4), Ing("Box of Nails", 3));
            AddFurn("CRAFT0095", "Standing Floor Lamp",    iconLamp,  "Tall brass floor lamp. Press [E] to toggle ON/OFF.", Ing("Scrap Steel Pipe", 3), Ing("Clean Glass Bottle", 2), Ing("Copper Wire Spool", 2));
            AddFurn("CRAFT0096", "Tall Bookshelf",         iconCrate, "Wooden bookshelf with hidden storage (18 slots).", Ing("Wood Log", 7), Ing("Box of Nails", 3), Ing("Industrial Glue", 2));
            AddFurn("CRAFT0097", "Wooden Dining Table",    iconBench, "Classic wooden dining table.", Ing("Wood Log", 6), Ing("Box of Nails", 3));
            AddFurn("CRAFT0098", "Coffee Table",           iconBench, "Low wooden living-room coffee table.", Ing("Wood Log", 4), Ing("Box of Nails", 2));
            AddFurn("CRAFT0099", "Kitchen Counter",        iconCrate, "Kitchen counter with sink and storage (18 slots).", Ing("Wood Log", 6), Ing("Scrap Metal Plate", 3), Ing("Scrap Steel Pipe", 2));
            AddFurn("CRAFT0100", "First Aid Station",      iconUtil,  "Emergency medical station. Press [E] to heal +25 HP.", Ing("Scrap Metal Plate", 4), Ing("Torn Infected Cloth", 5), Ing("Duct Tape", 3));
            AddFurn("CRAFT0101", "Weapon Rack",            iconCrate, "Armory rack for storing weapons and ammo (24 slots).", Ing("Wood Log", 5), Ing("Scrap Metal Plate", 5), Ing("Scrap Steel Pipe", 2));
            AddFurn("CRAFT0102", "Heater Stove",           iconPot,   "Cast-iron wood-burning stove. Press [E] to toggle.", Ing("Scrap Metal Plate", 5), Ing("Rough Stone", 4), Ing("Charcoal Lump", 4));
            AddFurn("CRAFT0103", "Retro CRT TV",           iconLamp,  "Old CRT television on stand. Press [E] to toggle.", Ing("Scrap Metal Plate", 4), Ing("Clean Glass Bottle", 3), Ing("Basic Logic Chip", 2), Ing("Copper Wire Spool", 2));
            AddFurn("CRAFT0104", "Woven Area Rug",         iconBed,   "Decorative woven floor rug.", Ing("Torn Infected Cloth", 6), Ing("Plant Fiber", 6), Ing("Sturdy Rope", 2));
            AddFurn("CRAFT0105", "Potted Fern",            iconUtil,  "Decorative terracotta pot with indoor fern.", Ing("Rough Stone", 3), Ing("Plant Fiber", 5));
            AddFurn("CRAFT0106", "Office Trash Bin",       iconCrate, "Metal trash bin for discarded items (18 slots).", Ing("Scrap Metal Plate", 3), Ing("Box of Nails", 2));

            if (initialResolved > 0) _furnitureIconsResolved = true;
            return _runtimeFurnitureRecipes;
        }

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
            if (string.IsNullOrEmpty(id)) return null;
            CraftingRecipe found = recipes != null
                ? recipes.Find(r => r != null && string.Equals(r.recipeId, id, StringComparison.OrdinalIgnoreCase))
                : null;
            if (found != null) return found;
            found = GetRuntimeFurnitureRecipes().Find(r => r != null && string.Equals(r.recipeId, id, StringComparison.OrdinalIgnoreCase));
            if (found != null) return found;
            return GetRuntimeArtifactRecipes().Find(r => r != null && string.Equals(r.recipeId, id, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// ดึงสูตรคราฟต์ตามชื่อ เช่น "Wooden Lantern Post"
        /// </summary>
        public CraftingRecipe GetRecipeByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            CraftingRecipe found = recipes != null
                ? recipes.Find(r => r != null && (string.Equals(r.recipeName, name, StringComparison.OrdinalIgnoreCase) || string.Equals(r.ResolvedResultName, name, StringComparison.OrdinalIgnoreCase)))
                : null;
            if (found != null) return found;
            found = GetRuntimeFurnitureRecipes().Find(r => r != null && (string.Equals(r.recipeName, name, StringComparison.OrdinalIgnoreCase) || string.Equals(r.ResolvedResultName, name, StringComparison.OrdinalIgnoreCase)));
            if (found != null) return found;
            return GetRuntimeArtifactRecipes().Find(r => r != null && (string.Equals(r.recipeName, name, StringComparison.OrdinalIgnoreCase) || string.Equals(r.ResolvedResultName, name, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        /// ดึงรายการสูตรคราฟต์ตามหมวดหมู่ (เช่น Building, Utility, Defense, Weapons, Consumables, Tools, Special, Artifact)
        /// </summary>
        public List<CraftingRecipe> GetRecipesByCategory(CraftingCategory category)
        {
            List<CraftingRecipe> result = new List<CraftingRecipe>();
            if (recipes != null)
            {
                foreach (CraftingRecipe recipe in recipes)
                {
                    if (recipe != null && recipe.category == category)
                    {
                        result.Add(recipe);
                    }
                }
            }

            if (category == CraftingCategory.Utility)
            {
                result.AddRange(GetRuntimeFurnitureRecipes());
            }

            if (category == CraftingCategory.Artifact)
            {
                result.AddRange(GetRuntimeArtifactRecipes());
            }

            return result;
        }

        /// <summary>
        /// ค้นหาสูตรคราฟต์ด้วยคำค้นหา (Search Bar) และกรองตามหมวดหมู่ได้
        /// </summary>
        public List<CraftingRecipe> SearchRecipes(string searchQuery, CraftingCategory? categoryFilter = null)
        {
            List<CraftingRecipe> result = new List<CraftingRecipe>();
            string query = string.IsNullOrWhiteSpace(searchQuery) ? string.Empty : searchQuery.Trim();

            void AppendMatching(List<CraftingRecipe> source)
            {
                if (source == null) return;
                for (int i = 0; i < source.Count; i++)
                {
                    CraftingRecipe recipe = source[i];
                    if (recipe == null) continue;
                    if (categoryFilter.HasValue && recipe.category != categoryFilter.Value) continue;

                    if (string.IsNullOrEmpty(query) ||
                        (!string.IsNullOrEmpty(recipe.recipeName) && recipe.recipeName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (!string.IsNullOrEmpty(recipe.ResolvedResultName) && recipe.ResolvedResultName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (!string.IsNullOrEmpty(recipe.description) && recipe.description.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        result.Add(recipe);
                    }
                }
            }

            AppendMatching(recipes);
            if (!categoryFilter.HasValue || categoryFilter.Value == CraftingCategory.Utility)
            {
                AppendMatching(GetRuntimeFurnitureRecipes());
            }
            if (!categoryFilter.HasValue || categoryFilter.Value == CraftingCategory.Artifact)
            {
                AppendMatching(GetRuntimeArtifactRecipes());
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

using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// BuildingData — ข้อมูลและคุณสมบัติของสิ่งปลูกสร้างแต่ละชนิด
    /// กำหนด Prefab, ขนาดพื้นที่วาง (Footprint), เลือดสูงสุด (Max HP), ระยะวาง และวัตถุดิบที่ใช้
    /// </summary>
    [CreateAssetMenu(fileName = "NewBuildingData", menuName = "TheLastEmpire/Building Data")]
    public class BuildingData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("รหัสเฉพาะของสิ่งปลูกสร้าง เช่น BUILD0001_Campfire")]
        public string buildingId;

        [Tooltip("ชื่อสิ่งปลูกสร้างที่แสดงในเกม")]
        public string buildingName;

        [Tooltip("หมวดหมู่สิ่งปลูกสร้าง (Building, Utility, Defense)")]
        public CraftingCategory category = CraftingCategory.Building;

        [Tooltip("รูปไอคอนของสิ่งปลูกสร้าง")]
        public Sprite icon;

        [TextArea(2, 4)]
        [Tooltip("คำอธิบายสิ่งปลูกสร้าง")]
        public string description;

        [Header("Prefabs & Visuals")]
        [Tooltip("Prefab ตัวจริงที่จะถูกสร้างลงในฉาก")]
        public GameObject prefab;

        [Tooltip("Prefab สำหรับเงาพรีวิวโปร่งใส (หากเว้นว่าง ระบบจะคัดลอก Mesh จาก Prefab ตัวจริงให้อัตโนมัติ)")]
        public GameObject ghostPrefab;

        [Header("Placement Settings")]
        [Tooltip("ขนาดกล่องพื้นที่วาง (กว้าง X, สูง Y, ลึก Z) สำหรับเช็คการชนตอนวาง")]
        public Vector3 footprintSize = new Vector3(1.5f, 1.5f, 1.5f);

        [Tooltip("ระยะห่างสูงสุดจากตัวผู้เล่นที่อนุญาตให้วางได้ (เมตร)")]
        [Min(1f)]
        public float buildRange = 7.0f;

        [Tooltip("เปิดใช้การ Snap ตำแหน่งตามตาราง Grid หรือไม่")]
        public bool snapToGrid = false;

        [Tooltip("ขนาดช่องตาราง Grid เมื่อเปิด Snap (เมตร)")]
        public float gridSize = 1.0f;

        [Header("Durability & Health")]
        [Tooltip("พลังชีวิตสูงสุดของสิ่งปลูกสร้าง")]
        [Min(1f)]
        public float maxHealth = 250f;

        [Tooltip("เปอร์เซ็นต์ลดความเสียหาย (0 = รับเต็ม, 0.5 = ลดลง 50%)")]
        [Range(0f, 0.9f)]
        public float damageResistance = 0f;

        [Header("Crafting Requirements (วัตถุดิบที่ใช้)")]
        [Tooltip("รายการวัตถุดิบที่ต้องใช้ในการสร้าง (ดึงมาจากสูตรคราฟต์หรือกำหนดเฉพาะ)")]
        public List<CraftingIngredient> ingredients = new List<CraftingIngredient>();

        public void CopyFromRecipe(CraftingRecipe recipe)
        {
            if (recipe == null) return;
            if (string.IsNullOrEmpty(buildingId)) buildingId = recipe.recipeId;
            if (string.IsNullOrEmpty(buildingName)) buildingName = recipe.recipeName;
            category = recipe.category;
            description = recipe.description;
            if (recipe.ingredients != null)
            {
                ingredients = new List<CraftingIngredient>(recipe.ingredients);
            }
        }
    }
}

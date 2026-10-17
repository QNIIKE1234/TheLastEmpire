using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TheLastEmpire.Editor
{
    [InitializeOnLoad]
    public static class CharacterAssetCreator
    {
        private const string CharDir = "Assets/Resources/Database/Characters";
        private const string DbDir = "Assets/Resources/Database";
        private const string DatabaseAssetPath = "Assets/Resources/Database/CharacterDatabase.asset";

        static CharacterAssetCreator()
        {
            EditorApplication.delayCall += EnsureAllCharacterAssetsCreated;
        }

        [MenuItem("The Last Empire/Generate 5 Playable Character Assets", priority = 2)]
        public static void EnsureAllCharacterAssetsCreated()
        {
            if (!Directory.Exists(CharDir))
            {
                Directory.CreateDirectory(CharDir);
                AssetDatabase.Refresh();
            }

            List<CharacterData> createdList = new List<CharacterData>();

            // 1. Vanguard
            createdList.Add(CreateOrUpdateCharacter(
                "CHAR_01_Vanguard", "VANGUARD", "The Vanguard", "Imperial Vanguard",
                "All-Rounder / Frontline",
                "ทหารผ่านศึกผู้ช่ำชองการต่อสู้ สมดุลทุกระยะ ตื่นขึ้นมาในซากป้อมปราการพร้อมคำสาบานประจำหน่วยที่ก้องอยู่ในหัว",
                "\"หัวฉันระบมไปหมด... พลขับกับคนอื่นหายไปไหนกันหมด? ปืนยังขึ้นลำอยู่ ต้องรีบมุ่งหน้าไปใจกลางเมืองก่อนที่ทุกอย่างจะสายไป\"",
                new Color(0.95f, 0.55f, 0.15f, 1f),
                100f, 100f, 5.0f, 10, 10, 1.0f,
                15f, 0.25f, 0.8f, 0f, 100, 60,
                new List<string> { "Rifle", "Knife" }, 0f, 1.0f,
                ShiftAbilityType.AdrenalineRush, "Adrenaline Rush",
                "กด Shift ค้างเพื่อสูบฉีดอะดรีนาลีน: วิ่งเร็ว 1.75 เท่า, ตี/ยิงไวขึ้น 30%, ลดความเสียหายลง 20% (กินค่าความหิวไวขึ้น)",
                0f
            ));

            // 2. Juggernaut
            createdList.Add(CreateOrUpdateCharacter(
                "CHAR_02_Juggernaut", "JUGGERNAUT", "The Juggernaut", "Ironbound Behemoth",
                "Melee Brawler / Crowd Control",
                "มนุษย์ร่างยักษ์ผู้ผ่านการดัดแปลงพันธุกรรมและโครงสร้างเหล็ก ตื่นขึ้นมาในซากโรงงานอุตสาหกรรมใต้ดิน พร้อมความแค้นต่อจักรวรรดิ",
                "\"โซ่ตรวนขาดแล้ว... พวกมันทดลองอะไรกับร่างกายข้า? ร่างกายนี้ไม่รู้สึกเจ็บอีกต่อไป ข้าจะพังทุกอย่างที่ขวางหน้า\"",
                new Color(0.92f, 0.22f, 0.22f, 1f),
                150f, 120f, 4.3f, 15, 8, 1.2f,
                12f, 0.25f, 1.0f, 0.15f, 50, 20,
                new List<string> { "Sledgehammer" }, 0f, 1.0f,
                ShiftAbilityType.UnstoppableCharge, "Unstoppable Charge",
                "กด Shift พุ่งชนทะลวงด้วยความเร็วสูง สร้างดาเมจชน 25 + สตั๊น 1 วิ บล็อกและสะท้อนกระสุนปืนทั้งหมด คูลดาวน์ 5 วิ",
                5.0f
            ));

            // 3. Phantom
            createdList.Add(CreateOrUpdateCharacter(
                "CHAR_03_Phantom", "PHANTOM", "The Phantom", "Shadow Assassin",
                "Hit-and-Run / Assassin",
                "มือสังหารหน่วยลอบเร้นมิติเงา ตื่นขึ้นมาในเงามืดของตรอกซอยแคบ พร้อมภารกิจลับที่สั่งให้ปิดปากบุคคลสำคัญก่อนโลกล่มสลาย",
                "\"เป้าหมายของฉัน... ดอกเตอร์คนนั้นอยู่ที่ไหน? จักรวรรดิต้องการปิดปากเขาทำไมกันแน่? มีเงื่อนงำบางอย่างที่ถูกซ่อนไว้\"",
                new Color(0.66f, 0.33f, 0.97f, 1f),
                75f, 90f, 6.0f, 12, 10, 1.0f,
                18f, 0.30f, 0.55f, 0f, 150, 40,
                new List<string> { "Katana", "Pistol" }, 0f, 1.0f,
                ShiftAbilityType.ShadowStep, "Shadow Step",
                "กด Shift ค้างเพื่อเข้าสู่ร่างเงา: วิ่งเร็วขึ้น 40%, วิ่งทะลุผ่านตัวศัตรูได้โดยตรง, การโจมตีแรกหลังออกจากเงาการันตี Critical 2.0x",
                0f
            ));

            // 4. Marksman
            createdList.Add(CreateOrUpdateCharacter(
                "CHAR_04_Marksman", "MARKSMAN", "The Marksman", "Apex Sharpshooter",
                "Long-Range / Tactical",
                "พลซุ่มยิงสายตานกอินทรี ตื่นขึ้นมาบนยอดหอคอยสูงขอบเมือง จำภาพสุดท้ายผ่านลำกล้องสไนเปอร์ในวินาทีที่เตาปฏิกรณ์ระเบิดได้แม่นยำ",
                "\"ภาพในลำกล้องวันนั้น... คนที่กดสวิตช์ไม่ใช่ศัตรูภายนอก แต่เป็นคนของจักรวรรดิเอง! ฉันต้องสืบหาเบื้องหลังการทรยศนี้\"",
                new Color(0.0f, 0.75f, 0.95f, 1f),
                85f, 100f, 4.8f, 7, 14, 1.0f,
                14f, 0.25f, 0.8f, 0f, 80, 30,
                new List<string> { "Sniper Rifle", "Revolver" }, 0f, 1.0f,
                ShiftAbilityType.EagleEye, "Eagle Eye",
                "กด Shift ค้างเพื่อเพ่งสมาธิ: กล้องซูมกว้าง 1.35x, ระยะยิงปืนไกลขึ้น 2 เท่า, กระสุนเจาะทะลุศัตรู (เดินช้าลงขณะเล็ง)",
                0f
            ));

            // 5. Scavenger
            createdList.Add(CreateOrUpdateCharacter(
                "CHAR_05_Scavenger", "SCAVENGER", "The Scavenger", "Wasteland Nomad",
                "Crowd Control / Resource Hoarder",
                "นักคุ้ยขยะแดนร้างสวมฮู้ด (ตัวละครหน้า Main Menu) คนนอกกำแพงเมืองที่แอบเข้ามาหาเศษอาหารประทังชีวิต แล้วติดอยู่ในซากอารยธรรม",
                "\"ท้องร้องดังจนแสบไส้... ต้องหาขนมปังหรืออะไรก็ได้กินก่อน ฉันไม่สนเรื่องจักรวรรดิอะไรทั้งนั้น ขอแค่หาทางเปิดประตูเมืองหนีรอดไปได้ก็พอ\"",
                new Color(0.25f, 0.85f, 0.45f, 1f),
                90f, 110f, 5.2f, 10, 10, 0.85f,
                16f, 0.25f, 0.75f, 0f, 300, 0,
                new List<string> { "Crossbow", "Survival Spear" }, 0.25f, 1.30f,
                ShiftAbilityType.ToxicTrail, "Toxic Trail",
                "กด Shift วิ่งทิ้งควันพิษสะกดรอย: ศัตรูที่เหยียบสโลว์ 50% + โดนพิษต่อเนื่อง 8 Dps (Passive: กินอาหารฟื้นฟูเลือดแถม +10 HP)",
                0f
            ));

            // Create or update CharacterDatabase.asset
            CharacterDatabase db = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(DatabaseAssetPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<CharacterDatabase>();
                AssetDatabase.CreateAsset(db, DatabaseAssetPath);
            }

            db.Characters.Clear();
            foreach (var ch in createdList)
            {
                if (ch != null && !db.Characters.Contains(ch))
                {
                    db.Characters.Add(ch);
                }
            }

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"<color=#50FA7B>[CharacterAssetCreator]</color> Successfully initialized 5 playable characters in: {DatabaseAssetPath}");
        }

        private static CharacterData CreateOrUpdateCharacter(
            string assetName, string id, string name, string title, string role,
            string lore, string clue, Color theme,
            float hp, float hunger, float speed, int meleeDmg, int rangedDmg, float hungerMultiplier,
            float dashSpeed, float dashDuration, float dashCooldown, float dmgReduction, int money, int pistolAmmo,
            List<string> weapons, float dropBonus, float moneyMult,
            ShiftAbilityType shiftType, string shiftName, string shiftDesc, float shiftCd)
        {
            string path = $"{CharDir}/{assetName}.asset";
            CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<CharacterData>();
                AssetDatabase.CreateAsset(data, path);
            }

            data.characterId = id;
            data.characterName = name;
            data.characterTitle = title;
            data.roleCategory = role;
            data.loreSynopsis = lore;
            data.amnesiaWakingClue = clue;
            data.themeColor = theme;

            data.maxHealth = hp;
            data.maxHunger = hunger;
            data.moveSpeed = speed;
            data.baseMeleeDamage = meleeDmg;
            data.baseRangedDamage = rangedDmg;
            data.hungerConsumptionMultiplier = hungerMultiplier;

            data.dashSpeed = dashSpeed;
            data.dashDuration = dashDuration;
            data.dashCooldown = dashCooldown;
            data.damageReductionPercent = dmgReduction;

            data.startingMoney = money;
            data.startingPistolAmmo = pistolAmmo;
            data.startingWeapons = weapons;
            data.dropRateBonus = dropBonus;
            data.moneyMultiplier = moneyMult;

            data.shiftAbilityType = shiftType;
            data.shiftAbilityName = shiftName;
            data.shiftAbilityDescription = shiftDesc;
            data.shiftCooldown = shiftCd;

            EditorUtility.SetDirty(data);
            return data;
        }
    }
}

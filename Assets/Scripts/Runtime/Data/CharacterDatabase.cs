using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    [CreateAssetMenu(fileName = "CharacterDatabase", menuName = "The Last Empire/Character Database", order = 2)]
    public class CharacterDatabase : ScriptableObject
    {
        [Header("Registered Playable Characters")]
        [SerializeField] private List<CharacterData> characters = new List<CharacterData>();

        private static CharacterDatabase _cachedInstance;

        public List<CharacterData> Characters => characters;

        public static CharacterDatabase Instance
        {
            get
            {
                if (_cachedInstance == null)
                {
                    _cachedInstance = Resources.Load<CharacterDatabase>("Database/CharacterDatabase");
                    if (_cachedInstance == null)
                    {
                        var all = Resources.LoadAll<CharacterDatabase>("");
                        if (all != null && all.Length > 0)
                        {
                            _cachedInstance = all[0];
                        }
                    }
                }

                if (_cachedInstance == null || _cachedInstance.characters == null || _cachedInstance.characters.Count == 0)
                {
                    _cachedInstance = ScriptableObject.CreateInstance<CharacterDatabase>();
                    _cachedInstance.characters = CreateDefaultRoster();
                }

                return _cachedInstance;
            }
        }

        public CharacterData GetCharacter(string id)
        {
            if (string.IsNullOrEmpty(id)) return characters.Count > 0 ? characters[0] : null;

            foreach (var ch in characters)
            {
                if (ch != null && ch.characterId.Equals(id, System.StringComparison.OrdinalIgnoreCase))
                {
                    return ch;
                }
            }

            return characters.Count > 0 ? characters[0] : null;
        }

        public static List<CharacterData> CreateDefaultRoster()
        {
            List<CharacterData> roster = new List<CharacterData>();

            // 1. Vanguard
            var vanguard = ScriptableObject.CreateInstance<CharacterData>();
            vanguard.characterId = "VANGUARD";
            vanguard.characterName = "The Vanguard";
            vanguard.characterTitle = "Imperial Vanguard // กองหน้าทหารผ่านศึก";
            vanguard.roleCategory = "All-Rounder / Frontline";
            vanguard.loreSynopsis = "ทหารผ่านศึกผู้ช่ำชองการต่อสู้ สมดุลทุกระยะ ตื่นขึ้นมาในซากป้อมปราการพร้อมคำสาบานประจำหน่วยที่ก้องอยู่ในหัว";
            vanguard.amnesiaWakingClue = "\"หัวฉันระบมไปหมด... พลขับกับคนอื่นหายไปไหนกันหมด? ปืนยังขึ้นลำอยู่ ต้องรีบมุ่งหน้าไปใจกลางเมืองก่อนที่ทุกอย่างจะสายไป\"";
            vanguard.themeColor = new Color(0.95f, 0.55f, 0.15f, 1f);
            vanguard.maxHealth = 100f;
            vanguard.maxHunger = 100f;
            vanguard.moveSpeed = 5.0f;
            vanguard.baseMeleeDamage = 10;
            vanguard.baseRangedDamage = 10;
            vanguard.hungerConsumptionMultiplier = 1.0f;
            vanguard.dashSpeed = 15f;
            vanguard.dashDuration = 0.25f;
            vanguard.dashCooldown = 0.8f;
            vanguard.damageReductionPercent = 0f;
            vanguard.startingMoney = 100;
            vanguard.startingPistolAmmo = 60;
            vanguard.startingWeapons = new List<string> { "Rifle", "Knife" };
            vanguard.shiftAbilityType = ShiftAbilityType.AdrenalineRush;
            vanguard.shiftAbilityName = "Adrenaline Rush";
            vanguard.shiftAbilityDescription = "กด Shift ค้างเพื่อสูบฉีดอะดรีนาลีน: วิ่งเร็ว 1.75 เท่า, ตี/ยิงไวขึ้น 30%, ลดความเสียหายลง 20% (กินค่าความหิวไวขึ้น)";
            vanguard.shiftCooldown = 0f;
            roster.Add(vanguard);

            // 2. Juggernaut
            var juggernaut = ScriptableObject.CreateInstance<CharacterData>();
            juggernaut.characterId = "JUGGERNAUT";
            juggernaut.characterName = "The Juggernaut";
            juggernaut.characterTitle = "Ironbound Behemoth // จอมพลังเกราะเหล็ก";
            juggernaut.roleCategory = "Melee Brawler / Tank";
            juggernaut.loreSynopsis = "มนุษย์ร่างยักษ์ผู้ผ่านการดัดแปลงพันธุกรรมและโครงสร้างเหล็ก ตื่นขึ้นมาในซากโรงงานอุตสาหกรรมใต้ดิน พร้อมความแค้นต่อจักรวรรดิ";
            juggernaut.amnesiaWakingClue = "\"โซ่ตรวนขาดแล้ว... พวกมันทดลองอะไรกับร่างกายข้า? ร่างกายนี้ไม่รู้สึกเจ็บอีกต่อไป ข้าจะพังทุกอย่างที่ขวางหน้า\"";
            juggernaut.themeColor = new Color(0.92f, 0.22f, 0.22f, 1f);
            juggernaut.maxHealth = 150f;
            juggernaut.maxHunger = 120f;
            juggernaut.moveSpeed = 4.3f;
            juggernaut.baseMeleeDamage = 15;
            juggernaut.baseRangedDamage = 8;
            juggernaut.hungerConsumptionMultiplier = 1.2f;
            juggernaut.dashSpeed = 12f;
            juggernaut.dashDuration = 0.25f;
            juggernaut.dashCooldown = 1.0f;
            juggernaut.damageReductionPercent = 0.15f;
            juggernaut.startingMoney = 50;
            juggernaut.startingPistolAmmo = 20;
            juggernaut.startingWeapons = new List<string> { "Sledgehammer", "Shotgun" };
            juggernaut.shiftAbilityType = ShiftAbilityType.UnstoppableCharge;
            juggernaut.shiftAbilityName = "Unstoppable Charge";
            juggernaut.shiftAbilityDescription = "กด Shift พุ่งชนทะลวงด้วยความเร็วสูง สร้างดาเมจชน 25 + สตั๊น 1 วิ บล็อกและสะท้อนกระสุนปืนทั้งหมด คูลดาวน์ 5 วิ (Passive: ลดดาเมจตลอดเวลา 15%)";
            juggernaut.shiftCooldown = 5.0f;
            roster.Add(juggernaut);

            // 3. Phantom
            var phantom = ScriptableObject.CreateInstance<CharacterData>();
            phantom.characterId = "PHANTOM";
            phantom.characterName = "The Phantom";
            phantom.characterTitle = "Shadow Assassin // มือสังหารเงามืด";
            phantom.roleCategory = "Hit-and-Run / Assassin";
            phantom.loreSynopsis = "มือสังหารหน่วยลอบเร้นมิติเงา ตื่นขึ้นมาในเงามืดของตรอกซอยแคบ พร้อมภารกิจลับที่สั่งให้ปิดปากบุคคลสำคัญก่อนโลกล่มสลาย";
            phantom.amnesiaWakingClue = "\"เป้าหมายของฉัน... ดอกเตอร์คนนั้นอยู่ที่ไหน? จักรวรรดิต้องการปิดปากเขาทำไมกันแน่? มีเงื่อนงำบางอย่างที่ถูกซ่อนไว้\"";
            phantom.themeColor = new Color(0.66f, 0.33f, 0.97f, 1f);
            phantom.maxHealth = 75f;
            phantom.maxHunger = 90f;
            phantom.moveSpeed = 6.0f;
            phantom.baseMeleeDamage = 12;
            phantom.baseRangedDamage = 10;
            phantom.hungerConsumptionMultiplier = 1.0f;
            phantom.dashSpeed = 18f;
            phantom.dashDuration = 0.30f;
            phantom.dashCooldown = 0.55f;
            phantom.damageReductionPercent = 0f;
            phantom.startingMoney = 150;
            phantom.startingPistolAmmo = 40;
            phantom.startingWeapons = new List<string> { "Katana", "Pistol" };
            phantom.shiftAbilityType = ShiftAbilityType.ShadowStep;
            phantom.shiftAbilityName = "Shadow Step";
            phantom.shiftAbilityDescription = "กด Shift ค้างเพื่อเข้าสู่ร่างเงา: วิ่งเร็วขึ้น 40%, วิ่งทะลุผ่านตัวศัตรูได้โดยตรง, การโจมตีแรกหลังออกจากเงาการันตี Critical 2.0x";
            phantom.shiftCooldown = 0f;
            roster.Add(phantom);

            // 4. Marksman
            var marksman = ScriptableObject.CreateInstance<CharacterData>();
            marksman.characterId = "MARKSMAN";
            marksman.characterName = "The Marksman";
            marksman.characterTitle = "Apex Sharpshooter // พลแม่นปืนสไนเปอร์";
            marksman.roleCategory = "Long-Range / Tactical";
            marksman.loreSynopsis = "พลซุ่มยิงสายตานกอินทรี ตื่นขึ้นมาบนยอดหอคอยสูงขอบเมือง จำภาพสุดท้ายผ่านลำกล้องสไนเปอร์ในวินาทีที่เตาปฏิกรณ์ระเบิดได้แม่นยำ";
            marksman.amnesiaWakingClue = "\"ภาพในลำกล้องวันนั้น... คนที่กดสวิตช์ไม่ใช่ศัตรูภายนอก แต่เป็นคนของจักรวรรดิเอง! ฉันต้องสืบหาเบื้องหลังการทรยศนี้\"";
            marksman.themeColor = new Color(0.0f, 0.75f, 0.95f, 1f);
            marksman.maxHealth = 85f;
            marksman.maxHunger = 100f;
            marksman.moveSpeed = 4.8f;
            marksman.baseMeleeDamage = 7;
            marksman.baseRangedDamage = 14;
            marksman.hungerConsumptionMultiplier = 1.0f;
            marksman.dashSpeed = 14f;
            marksman.dashDuration = 0.25f;
            marksman.dashCooldown = 0.8f;
            marksman.damageReductionPercent = 0f;
            marksman.startingMoney = 80;
            marksman.startingPistolAmmo = 30;
            marksman.startingWeapons = new List<string> { "Sniper Rifle", "Revolver" };
            marksman.shiftAbilityType = ShiftAbilityType.EagleEye;
            marksman.shiftAbilityName = "Eagle Eye";
            marksman.shiftAbilityDescription = "กด Shift ค้างเพื่อเพ่งสมาธิ: กล้องซูมกว้าง 1.35x, ระยะยิงปืนไกลขึ้น 2 เท่า, กระสุนเจาะทะลุศัตรู (เดินช้าลงขณะเล็ง)";
            marksman.shiftCooldown = 0f;
            roster.Add(marksman);

            // 5. Scavenger
            var scavenger = ScriptableObject.CreateInstance<CharacterData>();
            scavenger.characterId = "SCAVENGER";
            scavenger.characterName = "The Scavenger";
            scavenger.characterTitle = "Wasteland Nomad // ผู้รอดชีวิตพเนจร";
            scavenger.roleCategory = "Survivalist / Trapper";
            scavenger.loreSynopsis = "นักคุ้ยขยะแดนร้างสวมฮู้ด คนนอกกำแพงเมืองที่แอบเข้ามาหาเศษอาหารประทังชีวิต แล้วติดอยู่ในซากอารยธรรม";
            scavenger.amnesiaWakingClue = "\"ท้องร้องดังจนแสบไส้... ต้องหาขนมปังหรืออะไรก็ได้กินก่อน ฉันไม่สนเรื่องจักรวรรดิอะไรทั้งนั้น ขอแค่หาทางเปิดประตูเมืองหนีรอดไปได้ก็พอ\"";
            scavenger.themeColor = new Color(0.25f, 0.85f, 0.45f, 1f);
            scavenger.maxHealth = 90f;
            scavenger.maxHunger = 110f;
            scavenger.moveSpeed = 5.2f;
            scavenger.baseMeleeDamage = 10;
            scavenger.baseRangedDamage = 10;
            scavenger.hungerConsumptionMultiplier = 0.85f;
            scavenger.dashSpeed = 16f;
            scavenger.dashDuration = 0.25f;
            scavenger.dashCooldown = 0.75f;
            scavenger.damageReductionPercent = 0f;
            scavenger.startingMoney = 300;
            scavenger.startingPistolAmmo = 0;
            scavenger.startingWeapons = new List<string> { "Crossbow", "Survival Spear" };
            scavenger.shiftAbilityType = ShiftAbilityType.ToxicTrail;
            scavenger.shiftAbilityName = "Toxic Trail";
            scavenger.shiftAbilityDescription = "กด Shift วิ่งทิ้งควันพิษสะกดรอย: ศัตรูที่เหยียบสโลว์ 50% + โดนพิษต่อเนื่อง 8 Dps (Passive: กินอาหารฟื้นฟูเลือดแถม +10 HP)";
            scavenger.shiftCooldown = 0f;
            roster.Add(scavenger);

            return roster;
        }
    }

    /// <summary>
    /// Global session manager holding the currently selected character ID across scenes.
    /// </summary>
    public static class CharacterManager
    {
        private const string PrefsKey = "TheLastEmpire_SelectedCharacterId";
        public static string SelectedCharacterId
        {
            get => PlayerPrefs.GetString(PrefsKey, "VANGUARD");
            set
            {
                PlayerPrefs.SetString(PrefsKey, value);
                PlayerPrefs.Save();
            }
        }

        public static CharacterData GetSelectedCharacter()
        {
            if (CharacterDatabase.Instance != null)
            {
                return CharacterDatabase.Instance.GetCharacter(SelectedCharacterId);
            }
            return null;
        }
    }
}

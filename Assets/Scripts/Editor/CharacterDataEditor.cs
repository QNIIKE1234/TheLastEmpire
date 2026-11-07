using UnityEditor;
using UnityEngine;

namespace TheLastEmpire.Editor
{
    [CustomEditor(typeof(CharacterData))]
    public class CharacterDataEditor : UnityEditor.Editor
    {
        private static bool showIdentity = true;
        private static bool showCombat = true;
        private static bool showSanity = true;
        private static bool showMobility = true;
        private static bool showHunger = true;
        private static bool showLoadout = true;
        private static bool showAbility = true;
        private static bool showPreview = true;

        public override void OnInspectorGUI()
        {
            CharacterData data = (CharacterData)target;
            serializedObject.Update();

            // 1. Header Banner
            DrawHeaderBanner(data);

            EditorGUILayout.Space(6);

            // 2. Identity & Lore Section
            showIdentity = DrawSectionFoldout("👤 Identity & Lore Dossier", showIdentity, new Color(0.2f, 0.6f, 1.0f));
            if (showIdentity)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("characterId"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("characterName"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("characterTitle"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("roleCategory"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("characterPortrait"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("characterModelPrefab"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("themeColor"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("amnesiaWakingClue"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("loreSynopsis"));
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }

            // 3. Combat & Attributes Section
            showCombat = DrawSectionFoldout("🛡️ Attributes & Combat Vitals", showCombat, new Color(0.95f, 0.4f, 0.3f));
            if (showCombat)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("maxHealth"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("maxHunger"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("baseMeleeDamage"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("baseRangedDamage"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("critChanceBonus"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("critMultiplierBonus"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("damageReductionPercent"));
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }

            // 4. Movement & Mobility Section
            showMobility = DrawSectionFoldout("🏃 Movement & Mobility Controls", showMobility, new Color(0.3f, 0.85f, 0.5f));
            if (showMobility)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("moveSpeed"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("sprintSpeedMultiplier"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("shootSprintLockDuration"));
                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("dashSpeed"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("dashDuration"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("dashCooldown"));
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }

            // 5. Hunger & Survival Dynamics Section
            showHunger = DrawSectionFoldout("🍞 Hunger & Survival Dynamics", showHunger, new Color(0.95f, 0.75f, 0.2f));
            if (showHunger)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("baseHungerDepletionRate"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("sprintHungerMultiplier"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("hungerConsumptionMultiplier"));
                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("starvationDamage"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("starvationInterval"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("foodHealBonus"));
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }

            // 6. Sanity & Mental Fortitude Section
            showSanity = DrawSectionFoldout("🧠 Sanity & Mental Fortitude", showSanity, new Color(0.2f, 0.8f, 0.95f));
            if (showSanity)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("maxSanity"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("sanityDepletionMultiplier"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("sanityDamageTakenPercent"));
                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("nightSanityDepletionRate"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("daySanityRecoveryRate"));
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }

            // 7. Starting Arsenal & Inventory Supplies
            showLoadout = DrawSectionFoldout("🎒 Starting Arsenal & Supplies", showLoadout, new Color(0.4f, 0.8f, 0.95f));
            if (showLoadout)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("startingMoney"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("startingRations"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("startingWeapons"), true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("startingAmmo"), true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("startingExtraItems"), true);
                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("dropRateBonus"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("moneyMultiplier"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("startingPistolAmmo"));
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }

            // 7. Signature Shift Active Ability
            showAbility = DrawSectionFoldout("⚡ Signature Shift Ability & Tuning", showAbility, new Color(0.8f, 0.45f, 1.0f));
            if (showAbility)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                SerializedProperty abilityTypeProp = serializedObject.FindProperty("shiftAbilityType");
                EditorGUILayout.PropertyField(abilityTypeProp);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("shiftAbilityName"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("shiftAbilityDescription"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("shiftCooldown"));

                EditorGUILayout.Space(6);
                ShiftAbilityType currentType = (ShiftAbilityType)abilityTypeProp.enumValueIndex;

                // Highlight parameters specific to the selected ability
                switch (currentType)
                {
                    case ShiftAbilityType.AdrenalineRush:
                        EditorGUILayout.LabelField("Adrenaline Rush Tuning Parameters", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("adrenalineSpeedMultiplier"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("adrenalineAttackSpeedBonus"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("adrenalineDamageReduction"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("adrenalineHungerDrainRate"));
                        break;

                    case ShiftAbilityType.UnstoppableCharge:
                        EditorGUILayout.LabelField("Unstoppable Charge Tuning Parameters", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("chargeSpeed"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("chargeDuration"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("chargeDamage"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("chargeStunDuration"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("chargeHungerCost"));
                        break;

                    case ShiftAbilityType.ShadowStep:
                        EditorGUILayout.LabelField("Shadow Step Tuning Parameters", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("shadowSpeedMultiplier"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("shadowHungerDrainRate"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("shadowExitCritMultiplier"));
                        break;

                    case ShiftAbilityType.EagleEye:
                        EditorGUILayout.LabelField("Eagle Eye Tuning Parameters", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("eagleEyeZoomMultiplier"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("eagleEyeRangeMultiplier"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("eagleEyeMoveSpeedMultiplier"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("eagleEyeHungerDrainRate"));
                        break;

                    case ShiftAbilityType.GrapplingHook:
                        EditorGUILayout.LabelField("Grappling Hook Tuning Parameters", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("grappleRange"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("grapplePullSpeed"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("grappleHungerCost"));
                        break;
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }

            // 8. Live UI Preview
            showPreview = DrawSectionFoldout("📋 Live Dossier UI Preview", showPreview, new Color(0.6f, 0.65f, 0.75f));
            if (showPreview)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Dossier Attributes Display:", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(data.GetFormattedAttributesString().Replace("<b>", "").Replace("</b>", ""), MessageType.None);
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Dossier Loadout Display:", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(data.GetFormattedLoadoutString().Replace("<b>", "").Replace("</b>", ""), MessageType.None);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHeaderBanner(CharacterData data)
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 62f);
            Color bannerBg = new Color(0.12f, 0.14f, 0.18f, 1f);
            EditorGUI.DrawRect(rect, bannerBg);

            // Left accent color strip
            Rect stripRect = new Rect(rect.x, rect.y, 6f, rect.height);
            EditorGUI.DrawRect(stripRect, data.themeColor);

            // Portrait thumbnail if available
            float portraitSize = 52f;
            if (data.characterPortrait != null && data.characterPortrait.texture != null)
            {
                Rect portraitRect = new Rect(rect.x + 12f, rect.y + 5f, portraitSize, portraitSize);
                GUI.DrawTexture(portraitRect, data.characterPortrait.texture, ScaleMode.ScaleToFit);
            }

            // Text info
            float textOffset = (data.characterPortrait != null) ? (portraitSize + 22f) : 16f;
            Rect titleRect = new Rect(rect.x + textOffset, rect.y + 8f, rect.width - textOffset - 10f, 22f);
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15,
                normal = { textColor = data.themeColor }
            };
            GUI.Label(titleRect, string.IsNullOrEmpty(data.characterName) ? "UNNAMED OPERATIVE" : data.characterName.ToUpper(), titleStyle);

            Rect subRect = new Rect(rect.x + textOffset, rect.y + 32f, rect.width - textOffset - 10f, 18f);
            GUIStyle subStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.75f, 0.8f, 0.9f) }
            };
            string subText = $"{data.characterTitle}  •  {data.roleCategory}  [{data.characterId}]";
            GUI.Label(subRect, subText, subStyle);
        }

        private bool DrawSectionFoldout(string title, bool state, Color barColor)
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 26f);
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.17f, 0.22f, 1f));

            Rect strip = new Rect(rect.x, rect.y, 4f, rect.height);
            EditorGUI.DrawRect(strip, barColor);

            GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                normal = { textColor = Color.white },
                onNormal = { textColor = Color.white }
            };

            Rect foldoutRect = new Rect(rect.x + 12f, rect.y + 4f, rect.width - 16f, 18f);
            return EditorGUI.Foldout(foldoutRect, state, title, true, foldoutStyle);
        }
    }
}


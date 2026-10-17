using UnityEngine;

namespace TheLastEmpire
{
    public class PlayerHUD : MonoBehaviour
    {
        [Header("UI References (Optional)")]
        [SerializeField] private UnityEngine.UI.Slider hpBarSliderHUD;
        [SerializeField] private UnityEngine.UI.Image hpBarFillHUD;
        [SerializeField] private UnityEngine.UI.Slider hungerSliderHUD;
        [SerializeField] private UnityEngine.UI.Image hungerBarFillHUD;
        [SerializeField] private UnityEngine.UI.Image weaponImage;
        [SerializeField] private UnityEngine.UI.Image ammoImage;
        [SerializeField] private TMPro.TMP_Text ammoTextHUD;
        [SerializeField] private TMPro.TMP_Text hpTextHUD;

        [Header("Display Settings")]
        [Tooltip("แสดงเฉพาะตัวเลขกระสุน (ไม่โชว์ชื่อปืน เช่น ตัด PISTOL: ออก)")]
        [SerializeField] private bool showWeaponName = false;

        [Header("Game Over References")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private TMPro.TMP_Text daysSurvivedText;
        [SerializeField] private UnityEngine.UI.Button retryButton;

        [Header("Game Over Timing & Fade Settings")]
        [Tooltip("หน่วงเวลาก่อนเริ่ม Fade (วินาที) ให้เห็นเหตุการณ์ในเกมดำเนินต่อไป เช่น ซอมบี้วิ่งรุม")]
        [SerializeField] private float deathDelayBeforeFade = 1.0f;
        [Tooltip("ระยะเวลาในการ Fade แผง GameOver ค่อยๆ สว่างขึ้นมา (วินาที)")]
        [SerializeField] private float gameOverFadeDuration = 1.2f;
        [Tooltip("หยุดเวลาในเกมตอนตายหรือไม่ (ถ้า false เกมจะเล่นต่อไปเรื่อยๆ ไม่หยุดเวลา)")]
        [SerializeField] private bool pauseGameOnDeath = false;
        [Tooltip("สุ่ม Seed แมพใหม่ตอน Retry หรือไม่ (ถ้า false จะเล่นซ้ำ Seed เดิมและเกิดที่ Safe Zone เดิม)")]
        [SerializeField] private bool randomizeSeedOnRetry = false;

        [Header("Player Reference (Optional)")]
        [SerializeField] private PlayerController player;

        private UnityEngine.UI.Slider _hpBarSlider;
        private UnityEngine.UI.Slider _hungerSlider;
        private TMPro.TMP_Text _ammoText;
        private UnityEngine.UI.Image _hpBarFill;
        private UnityEngine.UI.Image _hungerBarFill;
        private TMPro.TMP_Text _hpText;
        private TMPro.TMP_Text _hungerTextText;
        private UnityEngine.UI.Image _weaponImage;
        private UnityEngine.UI.Image _ammoImage;
        private TMPro.TMP_Text _charBadgeText;
        private TMPro.TMP_Text _shiftSkillText;

        private void Start()
        {
            // Auto-detect player in the scene/hierarchy if not manually assigned
            if (player == null)
            {
                player = Object.FindFirstObjectByType<PlayerController>();
            }

            SetupHUD();

            if (player != null)
            {
                player.OnAmmoChanged += UpdateHUD;
                player.OnHungerChanged += UpdateHUD;

                Health pHealth = player.GetComponent<Health>() ?? player.PlayerHealth;
                if (pHealth != null)
                {
                    pHealth.onHealthChanged.AddListener((hp) => UpdateHUD());
                    pHealth.onDeath.AddListener(ShowDeathResultScreen);
                    Debug.Log("[PlayerHUD] Successfully hooked into Player Health events.");
                }
                else
                {
                    Debug.LogError("[PlayerHUD] Could not find Health component on Player!");
                }
                
                // Initial update
                UpdateHUD();

                // Show the cinematic amnesia waking memory monologue banner
                StartCoroutine(ShowAmnesiaWakingRoutine());
            }
            else
            {
                Debug.LogWarning("[PlayerHUD] PlayerController reference not found. HUD will not receive state updates.");
            }

            if (retryButton != null)
            {
                retryButton.onClick.AddListener(RetryGame);
            }
        }

        private void Update()
        {
            UpdateShiftAbilityDisplay();
        }

        private void UpdateShiftAbilityDisplay()
        {
            if (_shiftSkillText == null || player == null) return;

            if (player.ActiveShiftAbility != null)
            {
                string skillName = player.ActiveCharacterData != null ? player.ActiveCharacterData.shiftAbilityName : "ABILITY";
                if (player.ActiveShiftAbility.IsActive)
                {
                    _shiftSkillText.text = $"<color=#00FFAA>✦ [SHIFT] {skillName.ToUpper()} (ACTIVE)</color>";
                }
                else if (player.ActiveShiftAbility.CooldownRemaining > 0.05f)
                {
                    _shiftSkillText.text = $"<color=#FFAA00>⏳ [SHIFT] {skillName.ToUpper()} ({player.ActiveShiftAbility.CooldownRemaining:F1}s)</color>";
                }
                else
                {
                    _shiftSkillText.text = $"<color=#C084FC>✦ [SHIFT] {skillName.ToUpper()} (READY)</color>";
                }
            }
        }

        private void OnDestroy()
        {
            if (player != null)
            {
                player.OnAmmoChanged -= UpdateHUD;
                player.OnHungerChanged -= UpdateHUD;

                Health pHealth = player.GetComponent<Health>() ?? player.PlayerHealth;
                if (pHealth != null)
                {
                    pHealth.onHealthChanged.RemoveListener((hp) => UpdateHUD());
                    pHealth.onDeath.RemoveListener(ShowDeathResultScreen);
                }
            }

            if (retryButton != null)
            {
                retryButton.onClick.RemoveListener(RetryGame);
            }
        }

        private void SetupHUD()
        {
            // 1. Check if user assigned their own UI references in Inspector
            if (hpBarSliderHUD != null || ammoTextHUD != null || hpBarFillHUD != null || hpTextHUD != null || 
                hungerSliderHUD != null || hungerBarFillHUD != null || weaponImage != null || ammoImage != null)
            {
                _hpBarSlider = hpBarSliderHUD;
                _hungerSlider = hungerSliderHUD;
                _ammoText = ammoTextHUD;
                _hpBarFill = hpBarFillHUD;
                _hungerBarFill = hungerBarFillHUD;
                _hpText = hpTextHUD;

                ResolveWeaponAndAmmoImages();
                return;
            }

            // 2. Fallback: Procedurally spawn a sleek, glassmorphic HUD panel
            Canvas hudCanvas = Object.FindFirstObjectByType<Canvas>();
            if (hudCanvas != null)
            {
                // Main glassmorphism container (Taller to hold Hunger bar and weapon icons)
                GameObject container = new GameObject("HUD_StatusPanel");
                container.transform.SetParent(hudCanvas.transform, false);

                RectTransform containerRect = container.AddComponent<RectTransform>();
                containerRect.anchorMin = new Vector2(0f, 0f); // Bottom-left
                containerRect.anchorMax = new Vector2(0f, 0f);
                containerRect.pivot = new Vector2(0f, 0f);
                containerRect.anchoredPosition = new Vector2(30f, 30f);
                containerRect.sizeDelta = new Vector2(410f, 220f);

                // Panel Background
                UnityEngine.UI.Image panelBg = container.AddComponent<UnityEngine.UI.Image>();
                panelBg.color = new Color(0.08f, 0.08f, 0.1f, 0.88f); // Sleek slate dark mode

                // Cyan Border/Outline
                GameObject borderObj = new GameObject("Border");
                borderObj.transform.SetParent(container.transform, false);
                UnityEngine.UI.Image borderImg = borderObj.AddComponent<UnityEngine.UI.Image>();
                borderImg.color = new Color(0f, 0.9f, 1f, 0.3f);
                RectTransform borderRect = borderObj.GetComponent<RectTransform>();
                borderRect.anchorMin = Vector2.zero;
                borderRect.anchorMax = Vector2.one;
                borderRect.offsetMin = new Vector2(-2f, -2f);
                borderRect.offsetMax = new Vector2(2f, 2f);
                borderObj.transform.SetAsFirstSibling();

                // 2.0 Operative Character Badge (Top of panel)
                GameObject badgeObj = new GameObject("CharacterBadge");
                badgeObj.transform.SetParent(container.transform, false);
                _charBadgeText = badgeObj.AddComponent<TMPro.TextMeshProUGUI>();
                _charBadgeText.fontSize = 13f;
                _charBadgeText.color = Color.white;
                _charBadgeText.fontStyle = TMPro.FontStyles.Bold;
                _charBadgeText.alignment = TMPro.TextAlignmentOptions.Left;
                RectTransform badgeRect = badgeObj.GetComponent<RectTransform>();
                badgeRect.anchorMin = new Vector2(0.05f, 0.86f);
                badgeRect.anchorMax = new Vector2(0.95f, 0.98f);
                badgeRect.offsetMin = Vector2.zero;
                badgeRect.offsetMax = Vector2.zero;

                // 2.1 HP Bar Background Track
                GameObject hpBarObj = new GameObject("HPBarTrack");
                hpBarObj.transform.SetParent(container.transform, false);

                RectTransform hpBarRect = hpBarObj.AddComponent<RectTransform>();
                hpBarRect.anchorMin = new Vector2(0.05f, 0.67f);
                hpBarRect.anchorMax = new Vector2(0.95f, 0.84f);
                hpBarRect.offsetMin = Vector2.zero;
                hpBarRect.offsetMax = Vector2.zero;

                UnityEngine.UI.Image hpBarTrack = hpBarObj.AddComponent<UnityEngine.UI.Image>();
                hpBarTrack.color = new Color(0.3f, 0.05f, 0.05f, 0.7f);

                // HP Bar Fill (Foreground)
                GameObject hpFillObj = new GameObject("HPBarFill");
                hpFillObj.transform.SetParent(hpBarObj.transform, false);

                _hpBarFill = hpFillObj.AddComponent<UnityEngine.UI.Image>();
                _hpBarFill.color = new Color(0.1f, 0.9f, 0.2f, 1f); // Glowing vibrant green

                RectTransform hpFillRect = hpFillObj.GetComponent<RectTransform>();
                hpFillRect.anchorMin = new Vector2(0f, 0f);
                hpFillRect.anchorMax = new Vector2(1f, 1f);
                hpFillRect.offsetMin = Vector2.zero;
                hpFillRect.offsetMax = Vector2.zero;

                // HP Text overlay
                GameObject hpTextObj = new GameObject("HPText");
                hpTextObj.transform.SetParent(hpBarObj.transform, false);

                _hpText = hpTextObj.AddComponent<TMPro.TextMeshProUGUI>();
                _hpText.fontSize = 17f;
                _hpText.color = Color.white;
                _hpText.fontStyle = TMPro.FontStyles.Bold;
                _hpText.alignment = TMPro.TextAlignmentOptions.Center;

                RectTransform hpTextRect = hpTextObj.GetComponent<RectTransform>();
                hpTextRect.anchorMin = Vector2.zero;
                hpTextRect.anchorMax = Vector2.one;
                hpTextRect.offsetMin = Vector2.zero;
                hpTextRect.offsetMax = Vector2.zero;

                // 2.2 Hunger Bar Background Track
                GameObject hungerBarObj = new GameObject("HungerBarTrack");
                hungerBarObj.transform.SetParent(container.transform, false);

                RectTransform hungerBarRect = hungerBarObj.AddComponent<RectTransform>();
                hungerBarRect.anchorMin = new Vector2(0.05f, 0.46f);
                hungerBarRect.anchorMax = new Vector2(0.95f, 0.63f);
                hungerBarRect.offsetMin = Vector2.zero;
                hungerBarRect.offsetMax = Vector2.zero;

                UnityEngine.UI.Image hungerBarTrack = hungerBarObj.AddComponent<UnityEngine.UI.Image>();
                hungerBarTrack.color = new Color(0.25f, 0.15f, 0.05f, 0.7f);

                // Hunger Bar Fill (Foreground)
                GameObject hungerFillObj = new GameObject("HungerBarFill");
                hungerFillObj.transform.SetParent(hungerBarObj.transform, false);

                _hungerBarFill = hungerFillObj.AddComponent<UnityEngine.UI.Image>();
                _hungerBarFill.color = new Color(0.95f, 0.75f, 0.2f, 1f); // Warm bread yellow

                RectTransform hungerFillRect = hungerFillObj.GetComponent<RectTransform>();
                hungerFillRect.anchorMin = new Vector2(0f, 0f);
                hungerFillRect.anchorMax = new Vector2(1f, 1f);
                hungerFillRect.offsetMin = Vector2.zero;
                hungerFillRect.offsetMax = Vector2.zero;

                // Hunger Text overlay
                GameObject hungerTextObj = new GameObject("HungerText");
                hungerTextObj.transform.SetParent(hungerBarObj.transform, false);

                _hungerTextText = hungerTextObj.AddComponent<TMPro.TextMeshProUGUI>();
                _hungerTextText.fontSize = 14f;
                _hungerTextText.color = Color.white;
                _hungerTextText.fontStyle = TMPro.FontStyles.Bold;
                _hungerTextText.alignment = TMPro.TextAlignmentOptions.Center;

                RectTransform hungerTextRect = hungerTextObj.GetComponent<RectTransform>();
                hungerTextRect.anchorMin = Vector2.zero;
                hungerTextRect.anchorMax = Vector2.one;
                hungerTextRect.offsetMin = Vector2.zero;
                hungerTextRect.offsetMax = Vector2.zero;

                // 2.3 Shift Ability Indicator
                GameObject shiftObj = new GameObject("ShiftSkillText");
                shiftObj.transform.SetParent(container.transform, false);
                _shiftSkillText = shiftObj.AddComponent<TMPro.TextMeshProUGUI>();
                _shiftSkillText.fontSize = 12f;
                _shiftSkillText.fontStyle = TMPro.FontStyles.Bold;
                _shiftSkillText.color = new Color(0.75f, 0.52f, 0.99f, 1f);
                _shiftSkillText.alignment = TMPro.TextAlignmentOptions.Left;
                RectTransform shiftRect = shiftObj.GetComponent<RectTransform>();
                shiftRect.anchorMin = new Vector2(0.05f, 0.28f);
                shiftRect.anchorMax = new Vector2(0.95f, 0.42f);
                shiftRect.offsetMin = Vector2.zero;
                shiftRect.offsetMax = Vector2.zero;

                // 2.4 Weapon Icon
                GameObject wpnObj = new GameObject("ProceduralWeaponImage");
                wpnObj.transform.SetParent(container.transform, false);
                _weaponImage = wpnObj.AddComponent<UnityEngine.UI.Image>();
                _weaponImage.preserveAspect = true;
                RectTransform wpnRect = wpnObj.GetComponent<RectTransform>();
                wpnRect.anchorMin = new Vector2(0.05f, 0.04f);
                wpnRect.anchorMax = new Vector2(0.05f, 0.04f);
                wpnRect.pivot = new Vector2(0f, 0f);
                wpnRect.anchoredPosition = Vector2.zero;
                wpnRect.sizeDelta = new Vector2(36f, 36f);

                // 2.5 Ammo Icon
                GameObject ammoIconObj = new GameObject("ProceduralAmmoImage");
                ammoIconObj.transform.SetParent(container.transform, false);
                _ammoImage = ammoIconObj.AddComponent<UnityEngine.UI.Image>();
                _ammoImage.preserveAspect = true;
                RectTransform aIconRect = ammoIconObj.GetComponent<RectTransform>();
                aIconRect.anchorMin = new Vector2(0.18f, 0.05f);
                aIconRect.anchorMax = new Vector2(0.18f, 0.05f);
                aIconRect.pivot = new Vector2(0f, 0f);
                aIconRect.anchoredPosition = Vector2.zero;
                aIconRect.sizeDelta = new Vector2(28f, 28f);

                // 2.6 Ammo Text
                GameObject ammoObj = new GameObject("AmmoTextHUD");
                ammoObj.transform.SetParent(container.transform, false);

                RectTransform ammoRect = ammoObj.AddComponent<RectTransform>();
                ammoRect.anchorMin = new Vector2(0.30f, 0.04f);
                ammoRect.anchorMax = new Vector2(0.95f, 0.24f);
                ammoRect.offsetMin = Vector2.zero;
                ammoRect.offsetMax = Vector2.zero;

                _ammoText = ammoObj.AddComponent<TMPro.TextMeshProUGUI>();
                _ammoText.fontSize = 20f;
                _ammoText.color = Color.white;
                _ammoText.fontStyle = TMPro.FontStyles.Bold;
                _ammoText.alignment = TMPro.TextAlignmentOptions.Left;
            }
        }

        private void ResolveWeaponAndAmmoImages()
        {
            _weaponImage = weaponImage;
            _ammoImage = ammoImage;

            // Smart detection: In case user dragged the GameObject named "Ammo" into weaponImage
            // and "Weapon" into ammoImage in the Inspector, auto-swap them to match intended targets!
            if (weaponImage != null && ammoImage != null)
            {
                string wName = weaponImage.gameObject.name.ToLower();
                string aName = ammoImage.gameObject.name.ToLower();

                if (wName.Contains("ammo") && aName.Contains("weapon"))
                {
                    _weaponImage = ammoImage;
                    _ammoImage = weaponImage;
                    Debug.Log("[PlayerHUD] Auto-corrected swapped Weapon and Ammo images based on GameObject names.");
                }
            }
        }

        private void UpdateHUD()
        {
            if (player == null) return;

            // 1. Update Health Bar
            Health pHealth = player.GetComponent<Health>() ?? player.PlayerHealth;
            if (pHealth != null)
            {
                float ratio = Mathf.Clamp01(pHealth.CurrentHealth / pHealth.MaxHealth);
                
                if (_hpBarSlider != null)
                {
                    _hpBarSlider.maxValue = pHealth.MaxHealth;
                    _hpBarSlider.value = pHealth.CurrentHealth;
                }
                else if (_hpBarFill != null)
                {
                    if (_hpBarFill.type == UnityEngine.UI.Image.Type.Filled)
                    {
                        _hpBarFill.fillAmount = ratio;
                    }
                    else
                    {
                        _hpBarFill.rectTransform.localScale = new Vector3(ratio, 1f, 1f);
                    }
                }

                if (_hpText != null)
                {
                    _hpText.text = $"HP: {Mathf.RoundToInt(pHealth.CurrentHealth)} / {Mathf.RoundToInt(pHealth.MaxHealth)}";
                }
            }

            // 2. Update Hunger Bar
            float hungerRatio = Mathf.Clamp01(player.CurrentHunger / player.MaxHunger);
            if (_hungerSlider != null)
            {
                _hungerSlider.maxValue = player.MaxHunger;
                _hungerSlider.value = player.CurrentHunger;
            }
            else if (_hungerBarFill != null)
            {
                if (_hungerBarFill.type == UnityEngine.UI.Image.Type.Filled)
                {
                    _hungerBarFill.fillAmount = hungerRatio;
                }
                else
                {
                    _hungerBarFill.rectTransform.localScale = new Vector3(hungerRatio, 1f, 1f);
                }
            }

            if (_hungerTextText != null)
            {
                _hungerTextText.text = $"HUNGER: {Mathf.RoundToInt(player.CurrentHunger)} / {Mathf.RoundToInt(player.MaxHunger)}";
            }

            // 3. Update Weapon & Ammo Visuals
            UpdateWeaponAndAmmoVisuals();

            // 4. Update Character Badge & Shift Ability
            if (_charBadgeText != null && player.ActiveCharacterData != null)
            {
                string hex = ColorUtility.ToHtmlStringRGB(player.ActiveCharacterData.themeColor);
                _charBadgeText.text = $"<color=#{hex}><b>[{player.ActiveCharacterData.characterName.ToUpper()}]</b></color> <size=11>{player.ActiveCharacterData.roleCategory.ToUpper()}</size>";
            }

            UpdateShiftAbilityDisplay();
        }

        private void UpdateWeaponAndAmmoVisuals()
        {
            if (player == null) return;

            string currentWeaponName = "";
            string currentAmmoType = "";
            Sprite weaponSprite = null;
            Sprite ammoSprite = null;

            if (player.CurrentWeapon != null && !string.IsNullOrEmpty(player.CurrentWeapon.weaponName))
            {
                currentWeaponName = player.CurrentWeapon.weaponName;
                currentAmmoType = player.CurrentWeapon.ammoType;
                weaponSprite = player.CurrentWeapon.icon;
            }
            else if (player.CurrentMeleeWeapon != null && !string.IsNullOrEmpty(player.CurrentMeleeWeapon.weaponName))
            {
                currentWeaponName = player.CurrentMeleeWeapon.weaponName;
                weaponSprite = player.CurrentMeleeWeapon.icon;
                currentAmmoType = null; // Melee weapons do not consume ammo
            }

            // Lookup weapon icon in ItemDatabase if not assigned on weapon object
            if (weaponSprite == null && !string.IsNullOrEmpty(currentWeaponName) && ItemDatabase.Instance != null)
            {
                ItemData wData = ItemDatabase.Instance.GetItemByName(currentWeaponName);
                if (wData != null)
                {
                    weaponSprite = wData.icon;
                }
            }

            // Lookup ammo icon in ItemDatabase if wielding a firearm
            if (player.CurrentWeapon != null)
            {
                if (string.IsNullOrEmpty(currentAmmoType))
                {
                    string lower = currentWeaponName.ToLower();
                    if (lower.Contains("rifl")) currentAmmoType = "Rifle Ammo";
                    else if (lower.Contains("shot")) currentAmmoType = "Shotgun Ammo";
                    else currentAmmoType = "Pistol Ammo";
                }

                if (!string.IsNullOrEmpty(currentAmmoType) && ItemDatabase.Instance != null)
                {
                    ItemData aData = ItemDatabase.Instance.GetItemByName(currentAmmoType);
                    if (aData != null)
                    {
                        ammoSprite = aData.icon;
                    }
                    else
                    {
                        // Fallback check generic Ammo
                        ItemData genericAmmo = ItemDatabase.Instance.GetItemByName("Ammo");
                        if (genericAmmo != null) ammoSprite = genericAmmo.icon;
                    }
                }
            }

            // Apply Weapon Image
            if (_weaponImage != null)
            {
                if (weaponSprite != null)
                {
                    _weaponImage.sprite = weaponSprite;
                    _weaponImage.enabled = true;
                    _weaponImage.preserveAspect = true;
                    _weaponImage.color = Color.white;
                }
                else
                {
                    _weaponImage.enabled = false;
                }
            }

            // Apply Ammo Image
            if (_ammoImage != null)
            {
                if (ammoSprite != null)
                {
                    _ammoImage.sprite = ammoSprite;
                    _ammoImage.enabled = true;
                    _ammoImage.preserveAspect = true;
                    _ammoImage.color = Color.white;
                }
                else
                {
                    _ammoImage.enabled = false;
                }
            }

            // Update Ammo / Weapon Text HUD
            if (_ammoText != null)
            {
                if (player.CurrentWeapon != null && !string.IsNullOrEmpty(player.CurrentWeapon.weaponName))
                {
                    string weaponPrefix = showWeaponName ? $"{player.CurrentWeaponName.ToUpper()}: " : "";
                    if (player.IsReloading)
                    {
                        _ammoText.text = $"{weaponPrefix}<color=yellow>RELOAD...</color> (<color=#cfd8dc>{player.CurrentReserveAmmo}</color>)";
                    }
                    else
                    {
                        _ammoText.text = $"{weaponPrefix}<color=#00e5ff>{player.CurrentMagazine}</color> / <color=#cfd8dc>{player.CurrentReserveAmmo}</color>";
                    }
                }
                else if (player.CurrentMeleeWeapon != null && !string.IsNullOrEmpty(player.CurrentMeleeWeapon.weaponName))
                {
                    string meleeName = showWeaponName ? $"{player.CurrentMeleeWeaponName.ToUpper()}: " : "";
                    _ammoText.text = $"{meleeName}<color=#a0e0a0>MELEE</color>";
                }
                else
                {
                    _ammoText.text = "<color=gray>-</color>";
                }
            }
        }

        private void ShowDeathResultScreen()
        {
            StartCoroutine(DeathSequenceCoroutine());
        }

        private System.Collections.IEnumerator DeathSequenceCoroutine()
        {
            // ถ้าไม่ได้ตั้งค่าให้หยุดเวลา เกมจะเล่นต่อไปเรื่อยๆ ซอมบี้และโลกจะขยับต่อตามปกติ
            if (pauseGameOnDeath)
            {
                Time.timeScale = 0f;
            }

            // รอสักแป๊บหนึ่งโดยที่เกมยังเล่นต่อไปเรื่อยๆ ให้เห็นจังหวะตัวละครล้ม/ซอมบี้รุม
            if (deathDelayBeforeFade > 0f)
            {
                yield return new WaitForSeconds(deathDelayBeforeFade);
            }

            // 2. จัดเตรียม CanvasGroup สำหรับ Fade In
            CanvasGroup canvasGroup = null;

            if (gameOverPanel != null)
            {
                canvasGroup = gameOverPanel.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameOverPanel.AddComponent<CanvasGroup>();
                }

                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                gameOverPanel.SetActive(true);

                if (daysSurvivedText != null)
                {
                    int survivedDays = DayNightManager.Instance != null ? DayNightManager.Instance.DayCount : 1;
                    daysSurvivedText.text = $"You survived: <color=yellow>{survivedDays}</color> Days";
                }
            }
            else
            {
                Canvas hudCanvas = Object.FindFirstObjectByType<Canvas>();
                if (hudCanvas != null)
                {
                    GameObject resultPanel = BuildProceduralDeathScreen(hudCanvas);
                    canvasGroup = resultPanel.GetComponent<CanvasGroup>();
                    if (canvasGroup == null)
                    {
                        canvasGroup = resultPanel.AddComponent<CanvasGroup>();
                    }
                    canvasGroup.alpha = 0f;
                    canvasGroup.interactable = false;
                    canvasGroup.blocksRaycasts = false;
                }
            }

            // 3. ค่อยๆ Fade ขึ้น GameOver Screen อย่างนุ่มนวล
            if (canvasGroup != null)
            {
                float elapsed = 0f;
                float duration = Mathf.Max(0.05f, gameOverFadeDuration);

                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
                    yield return null;
                }

                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        private GameObject BuildProceduralDeathScreen(Canvas hudCanvas)
        {
            // Fullscreen panel container
            GameObject resultPanel = new GameObject("DeathResultScreen");
            resultPanel.transform.SetParent(hudCanvas.transform, false);

            RectTransform rect = resultPanel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Dark semi-transparent background
            UnityEngine.UI.Image bg = resultPanel.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0.05f, 0.05f, 0.08f, 0.92f);

            // Title "GAME OVER"
            GameObject titleObj = new GameObject("GameOverTitle");
            titleObj.transform.SetParent(resultPanel.transform, false);
            TMPro.TextMeshProUGUI titleText = titleObj.AddComponent<TMPro.TextMeshProUGUI>();
            titleText.text = "<color=red>GAME OVER</color>";
            titleText.fontSize = 54f;
            titleText.fontStyle = TMPro.FontStyles.Bold;
            titleText.alignment = TMPro.TextAlignmentOptions.Center;
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchoredPosition = new Vector2(0f, 100f);
            titleRect.sizeDelta = new Vector2(400f, 80f);

            // Surviving days text
            GameObject daysObj = new GameObject("DaysSurvivedText");
            daysObj.transform.SetParent(resultPanel.transform, false);
            TMPro.TextMeshProUGUI daysText = daysObj.AddComponent<TMPro.TextMeshProUGUI>();
            int days = DayNightManager.Instance != null ? DayNightManager.Instance.DayCount : 1;
            daysText.text = $"You survived: <color=yellow>{days}</color> Days";
            daysText.fontSize = 28f;
            daysText.alignment = TMPro.TextAlignmentOptions.Center;
            RectTransform daysRect = daysObj.GetComponent<RectTransform>();
            daysRect.anchoredPosition = new Vector2(0f, 20f);
            daysRect.sizeDelta = new Vector2(400f, 50f);

            // Retry Button container
            GameObject buttonObj = new GameObject("RetryButton");
            buttonObj.transform.SetParent(resultPanel.transform, false);
            RectTransform btnRect = buttonObj.AddComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(180f, 50f);
            btnRect.anchoredPosition = new Vector2(-100f, -60f);

            UnityEngine.UI.Image btnImg = buttonObj.AddComponent<UnityEngine.UI.Image>();
            btnImg.color = new Color(0.15f, 0.15f, 0.2f, 1f);

            // Highlight border for button
            GameObject btnBorder = new GameObject("BtnBorder");
            btnBorder.transform.SetParent(buttonObj.transform, false);
            UnityEngine.UI.Image borderImg = btnBorder.AddComponent<UnityEngine.UI.Image>();
            borderImg.color = new Color(0f, 0.9f, 1f, 0.4f);
            RectTransform borderRect = btnBorder.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-2f, -2f);
            borderRect.offsetMax = new Vector2(2f, 2f);
            btnBorder.transform.SetAsFirstSibling();

            UnityEngine.UI.Button btn = buttonObj.AddComponent<UnityEngine.UI.Button>();
            btn.onClick.AddListener(RetryGame);

            // Button text
            GameObject btnTextObj = new GameObject("RetryText");
            btnTextObj.transform.SetParent(buttonObj.transform, false);
            TMPro.TextMeshProUGUI btnText = btnTextObj.AddComponent<TMPro.TextMeshProUGUI>();
            btnText.text = "Retry";
            btnText.fontSize = 22f;
            btnText.color = Color.white;
            btnText.fontStyle = TMPro.FontStyles.Bold;
            btnText.alignment = TMPro.TextAlignmentOptions.Center;
            RectTransform btnTextRect = btnTextObj.GetComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;

            // Main Menu Button container
            GameObject menuButtonObj = new GameObject("MainMenuButton");
            menuButtonObj.transform.SetParent(resultPanel.transform, false);
            RectTransform menuBtnRect = menuButtonObj.AddComponent<RectTransform>();
            menuBtnRect.sizeDelta = new Vector2(180f, 50f);
            menuBtnRect.anchoredPosition = new Vector2(100f, -60f);

            UnityEngine.UI.Image menuBtnImg = menuButtonObj.AddComponent<UnityEngine.UI.Image>();
            menuBtnImg.color = new Color(0.15f, 0.15f, 0.2f, 1f);

            GameObject menuBtnBorder = new GameObject("BtnBorder");
            menuBtnBorder.transform.SetParent(menuButtonObj.transform, false);
            UnityEngine.UI.Image menuBorderImg = menuBtnBorder.AddComponent<UnityEngine.UI.Image>();
            menuBorderImg.color = new Color(1f, 0.6f, 0.1f, 0.4f);
            RectTransform menuBorderRect = menuBtnBorder.GetComponent<RectTransform>();
            menuBorderRect.anchorMin = Vector2.zero;
            menuBorderRect.anchorMax = Vector2.one;
            menuBorderRect.offsetMin = new Vector2(-2f, -2f);
            menuBorderRect.offsetMax = new Vector2(2f, 2f);
            menuBtnBorder.transform.SetAsFirstSibling();

            UnityEngine.UI.Button menuBtn = menuButtonObj.AddComponent<UnityEngine.UI.Button>();
            menuBtn.onClick.AddListener(ReturnToMainMenu);

            GameObject menuBtnTextObj = new GameObject("MenuText");
            menuBtnTextObj.transform.SetParent(menuButtonObj.transform, false);
            TMPro.TextMeshProUGUI menuBtnText = menuBtnTextObj.AddComponent<TMPro.TextMeshProUGUI>();
            menuBtnText.text = "Main Menu";
            menuBtnText.fontSize = 22f;
            menuBtnText.color = Color.white;
            menuBtnText.fontStyle = TMPro.FontStyles.Bold;
            menuBtnText.alignment = TMPro.TextAlignmentOptions.Center;
            RectTransform menuBtnTextRect = menuBtnTextObj.GetComponent<RectTransform>();
            menuBtnTextRect.anchorMin = Vector2.zero;
            menuBtnTextRect.anchorMax = Vector2.one;
            menuBtnTextRect.offsetMin = Vector2.zero;
            menuBtnTextRect.offsetMax = Vector2.zero;

            return resultPanel;
        }

        public void ReturnToMainMenu()
        {
            Time.timeScale = 1f;

            if (GameManager.Instance != null) Destroy(GameManager.Instance.gameObject);
            if (WorldMapManager.Instance != null) Destroy(WorldMapManager.Instance.gameObject);
            if (DayNightManager.Instance != null) Destroy(DayNightManager.Instance.gameObject);
            if (InventoryUI.Instance != null) Destroy(InventoryUI.Instance.gameObject);

            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        public void RetryGame()
        {
            // Restore normal game speed
            Time.timeScale = 1f;

            // Reset day tracking status
            if (DayNightManager.Instance != null)
            {
                DayNightManager.Instance.ResetDayCount();
            }

            // Reset stage/exploration metrics
            if (WorldMapManager.Instance != null && WorldMapManager.Instance.MapGenerator != null)
            {
                WorldMapManager.Instance.ResetMapProgression();

                int seedToUse = randomizeSeedOnRetry 
                    ? Random.Range(1000, 99999) 
                    : WorldMapManager.Instance.MapGenerator.seed;

                // Re-initialize map progression at safe spawn point
                WorldMapManager.Instance.StartNewGame(seedToUse);
            }

            // Reload the current active scene
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            );
        }

        // =========================================================================
        //  Amnesia Awakening Monologue Routine
        // =========================================================================
        private System.Collections.IEnumerator ShowAmnesiaWakingRoutine()
        {
            if (player == null) yield break;

            CharacterData charData = player.ActiveCharacterData;
            if (charData == null)
            {
                charData = CharacterManager.GetSelectedCharacter();
            }
            if (charData == null) yield break;

            Canvas hudCanvas = Object.FindFirstObjectByType<Canvas>();
            if (hudCanvas == null) yield break;

            GameObject bannerObj = new GameObject("AmnesiaAwakeningBanner");
            bannerObj.transform.SetParent(hudCanvas.transform, false);

            RectTransform bannerRect = bannerObj.AddComponent<RectTransform>();
            bannerRect.anchorMin = new Vector2(0.5f, 1f);
            bannerRect.anchorMax = new Vector2(0.5f, 1f);
            bannerRect.pivot = new Vector2(0.5f, 1f);
            bannerRect.anchoredPosition = new Vector2(0f, -30f);
            bannerRect.sizeDelta = new Vector2(760f, 125f);

            CanvasGroup bannerGroup = bannerObj.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;

            // Background Glass Panel
            UnityEngine.UI.Image bgImg = bannerObj.AddComponent<UnityEngine.UI.Image>();
            bgImg.color = new Color(0.06f, 0.07f, 0.09f, 0.94f);

            // Border (Glow in character's theme color)
            GameObject borderObj = new GameObject("Border");
            borderObj.transform.SetParent(bannerObj.transform, false);
            UnityEngine.UI.Image borderImg = borderObj.AddComponent<UnityEngine.UI.Image>();
            borderImg.color = new Color(charData.themeColor.r, charData.themeColor.g, charData.themeColor.b, 0.85f);
            RectTransform bRect = borderObj.GetComponent<RectTransform>();
            bRect.anchorMin = Vector2.zero;
            bRect.anchorMax = Vector2.one;
            bRect.offsetMin = new Vector2(-2f, -2f);
            bRect.offsetMax = new Vector2(2f, 2f);
            borderObj.transform.SetAsFirstSibling();

            // Header Title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(bannerObj.transform, false);
            TMPro.TMP_Text titleText = titleObj.AddComponent<TMPro.TextMeshProUGUI>();
            titleText.text = $"❖ ความทรงจำสุดท้ายก่อนตื่น // {charData.characterName.ToUpper()} ❖";
            titleText.fontSize = 15;
            titleText.fontStyle = TMPro.FontStyles.Bold;
            titleText.color = charData.themeColor;
            titleText.alignment = TMPro.TextAlignmentOptions.Center;
            RectTransform tRect = titleObj.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0f, 1f);
            tRect.anchorMax = new Vector2(1f, 1f);
            tRect.pivot = new Vector2(0.5f, 1f);
            tRect.anchoredPosition = new Vector2(0f, -10f);
            tRect.sizeDelta = new Vector2(720f, 22f);

            // Subtitle
            GameObject subObj = new GameObject("Subtitle");
            subObj.transform.SetParent(bannerObj.transform, false);
            TMPro.TMP_Text subText = subObj.AddComponent<TMPro.TextMeshProUGUI>();
            subText.text = $"{charData.characterTitle}  •  {charData.roleCategory}";
            subText.fontSize = 11;
            subText.color = new Color(0.7f, 0.75f, 0.85f, 0.75f);
            subText.alignment = TMPro.TextAlignmentOptions.Center;
            RectTransform sRect = subObj.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0f, 1f);
            sRect.anchorMax = new Vector2(1f, 1f);
            sRect.pivot = new Vector2(0.5f, 1f);
            sRect.anchoredPosition = new Vector2(0f, -32f);
            sRect.sizeDelta = new Vector2(720f, 16f);

            // Clue Quote
            GameObject clueObj = new GameObject("Quote");
            clueObj.transform.SetParent(bannerObj.transform, false);
            TMPro.TMP_Text clueText = clueObj.AddComponent<TMPro.TextMeshProUGUI>();
            clueText.text = $"<i>{charData.amnesiaWakingClue}</i>";
            clueText.fontSize = 13;
            clueText.color = Color.white;
            clueText.alignment = TMPro.TextAlignmentOptions.Center;
            RectTransform cRect = clueObj.GetComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0f, 0f);
            cRect.anchorMax = new Vector2(1f, 1f);
            cRect.offsetMin = new Vector2(20f, 8f);
            cRect.offsetMax = new Vector2(-20f, -48f);

            // Smooth Fade In
            float elapsed = 0f;
            float fadeInDuration = 0.6f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                bannerGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
                yield return null;
            }
            bannerGroup.alpha = 1f;

            // Display for 6 seconds
            yield return new WaitForSeconds(6.0f);

            // Smooth Fade Out
            elapsed = 0f;
            float fadeOutDuration = 0.8f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                bannerGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
                yield return null;
            }

            Destroy(bannerObj);
        }
    }
}

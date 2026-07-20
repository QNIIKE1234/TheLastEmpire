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
        [SerializeField] private TMPro.TMP_Text ammoTextHUD;
        [SerializeField] private TMPro.TMP_Text hpTextHUD;

        [Header("Player Reference (Optional)")]
        [SerializeField] private PlayerController player;

        private UnityEngine.UI.Slider _hpBarSlider;
        private UnityEngine.UI.Slider _hungerSlider;
        private TMPro.TMP_Text _ammoText;
        private UnityEngine.UI.Image _hpBarFill;
        private UnityEngine.UI.Image _hungerBarFill;
        private TMPro.TMP_Text _hpText;
        private TMPro.TMP_Text _hungerTextText;

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
            }
            else
            {
                Debug.LogWarning("[PlayerHUD] PlayerController reference not found. HUD will not receive state updates.");
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
        }

        private void SetupHUD()
        {
            // 1. Check if user assigned their own UI references in Inspector
            if (hpBarSliderHUD != null || ammoTextHUD != null || hpBarFillHUD != null || hpTextHUD != null || hungerSliderHUD != null || hungerBarFillHUD != null)
            {
                _hpBarSlider = hpBarSliderHUD;
                _hungerSlider = hungerSliderHUD;
                _ammoText = ammoTextHUD;
                _hpBarFill = hpBarFillHUD;
                _hungerBarFill = hungerBarFillHUD;
                _hpText = hpTextHUD;
                return;
            }

            // 2. Fallback: Procedurally spawn a sleek, glassmorphic HUD panel
            Canvas hudCanvas = Object.FindFirstObjectByType<Canvas>();
            if (hudCanvas != null)
            {
                // Main glassmorphism container (Taller to hold Hunger bar)
                GameObject container = new GameObject("HUD_StatusPanel");
                container.transform.SetParent(hudCanvas.transform, false);

                RectTransform containerRect = container.AddComponent<RectTransform>();
                containerRect.anchorMin = new Vector2(0f, 0f); // Bottom-left
                containerRect.anchorMax = new Vector2(0f, 0f);
                containerRect.pivot = new Vector2(0f, 0f);
                containerRect.anchoredPosition = new Vector2(30f, 30f);
                containerRect.sizeDelta = new Vector2(360f, 170f);

                // Panel Background
                UnityEngine.UI.Image panelBg = container.AddComponent<UnityEngine.UI.Image>();
                panelBg.color = new Color(0.08f, 0.08f, 0.1f, 0.85f); // Sleek slate dark mode

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

                // 2.1 HP Bar Background Track
                GameObject hpBarObj = new GameObject("HPBarTrack");
                hpBarObj.transform.SetParent(container.transform, false);

                RectTransform hpBarRect = hpBarObj.AddComponent<RectTransform>();
                hpBarRect.anchorMin = new Vector2(0.05f, 0.65f);
                hpBarRect.anchorMax = new Vector2(0.95f, 0.88f);
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
                _hpText.fontSize = 18f;
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
                hungerBarRect.anchorMin = new Vector2(0.05f, 0.38f);
                hungerBarRect.anchorMax = new Vector2(0.95f, 0.60f);
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
                _hungerTextText.fontSize = 15f;
                _hungerTextText.color = Color.white;
                _hungerTextText.fontStyle = TMPro.FontStyles.Bold;
                _hungerTextText.alignment = TMPro.TextAlignmentOptions.Center;

                RectTransform hungerTextRect = hungerTextObj.GetComponent<RectTransform>();
                hungerTextRect.anchorMin = Vector2.zero;
                hungerTextRect.anchorMax = Vector2.one;
                hungerTextRect.offsetMin = Vector2.zero;
                hungerTextRect.offsetMax = Vector2.zero;

                // 2.3 Ammo Text
                GameObject ammoObj = new GameObject("AmmoTextHUD");
                ammoObj.transform.SetParent(container.transform, false);

                RectTransform ammoRect = ammoObj.AddComponent<RectTransform>();
                ammoRect.anchorMin = new Vector2(0.05f, 0.08f);
                ammoRect.anchorMax = new Vector2(0.95f, 0.32f);
                ammoRect.offsetMin = Vector2.zero;
                ammoRect.offsetMax = Vector2.zero;

                _ammoText = ammoObj.AddComponent<TMPro.TextMeshProUGUI>();
                _ammoText.fontSize = 24f;
                _ammoText.color = Color.white;
                _ammoText.fontStyle = TMPro.FontStyles.Bold;
                _ammoText.alignment = TMPro.TextAlignmentOptions.Left;
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

            // 3. Update Ammo Text
            if (_ammoText != null)
            {
                if (player.IsReloading)
                {
                    _ammoText.text = $"AMMO: <color=yellow>RELOADING...</color> (<color=#cfd8dc>{player.CurrentReserveAmmo}</color>)";
                }
                else
                {
                    _ammoText.text = $"AMMO: <color=#00e5ff>{player.CurrentMagazine}</color> / <color=#cfd8dc>{player.CurrentReserveAmmo}</color>";
                }
            }
        }

        private void ShowDeathResultScreen()
        {
            // Pause the game mechanics
            Time.timeScale = 0f;

            Canvas hudCanvas = Object.FindFirstObjectByType<Canvas>();
            if (hudCanvas == null) return;

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
            btnRect.sizeDelta = new Vector2(200f, 50f);
            btnRect.anchoredPosition = new Vector2(0f, -60f);

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
        }

        private void RetryGame()
        {
            // Restore normal game speed
            Time.timeScale = 1f;

            // Reset day tracking status
            if (DayNightManager.Instance != null)
            {
                DayNightManager.Instance.ResetDayCount();
            }

            // Reset stage/exploration metrics
            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.ResetMapProgression();
                // Seed a fresh randomized stage layout on reload
                WorldMapManager.Instance.StartNewGame(Random.Range(1000, 9999));
            }

            // Reload the current active scene
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            );
        }
    }
}

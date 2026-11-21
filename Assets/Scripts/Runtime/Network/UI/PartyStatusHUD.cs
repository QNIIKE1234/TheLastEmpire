using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastEmpire.Network
{
    /// <summary>
    /// PartyStatusHUD — แถบแสดงรายชื่อ อาชีพ หลอดเลือด และสถานะล้มรอชุบชีวิต (Downed & Revive)
    /// ของสมาชิกในปาร์ตี้ 1–6 คน ด้านขวาของหน้าจอ + ป้ายชื่อลอยเหนือหัวเพื่อนร่วมทีมในฉาก 3D
    /// + ป้ายนับถอยหลังเมื่อปาร์ตี้กำลังวาร์ปข้ามประตู (Party Portal Travel Banner)
    /// </summary>
    public class PartyStatusHUD : MonoBehaviour
    {
        public static PartyStatusHUD Instance { get; private set; }

        private GUIStyle _cardStyle;
        private GUIStyle _nameStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _bannerStyle;
        private GUIStyle _worldTagStyle;
        private Texture2D _whiteTex;
        private bool _stylesReady = false;

        private static readonly Color[] SlotColors = new[]
        {
            new Color(0.22f, 0.74f, 0.97f), // Slot 1: Cyan
            new Color(0.20f, 0.85f, 0.45f), // Slot 2: Emerald
            new Color(0.98f, 0.75f, 0.18f), // Slot 3: Amber
            new Color(0.95f, 0.38f, 0.58f), // Slot 4: Rose
            new Color(0.68f, 0.45f, 0.98f), // Slot 5: Violet
            new Color(1.00f, 0.52f, 0.20f)  // Slot 6: Orange
        };

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        private void EnsureStyles()
        {
            if (_stylesReady && _whiteTex != null) return;
            _stylesReady = true;

            _whiteTex = new Texture2D(1, 1);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply();

            _cardStyle = new GUIStyle(GUI.skin.box);

            _nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            _nameStyle.normal.textColor = Color.white;

            _statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            _statusStyle.normal.textColor = new Color(0.80f, 0.86f, 0.94f);

            _bannerStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _bannerStyle.normal.textColor = new Color(1f, 0.88f, 0.25f);

            _worldTagStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _worldTagStyle.normal.textColor = Color.white;
        }

        private void OnGUI()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName.IndexOf("Lobby", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                sceneName.IndexOf("Menu", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return;
            }

            EnsureStyles();

            int totalPlayers = PlayerRegistry.GetTotalPlayerCount();
            if (!EmpireNetworkManager.IsOnline && totalPlayers <= 1) return;

            DrawPartySidebarHUD();
            DrawTeammateOverheadWorldTags();
            DrawPartyPortalCountdownBanner();
        }

        private void DrawPartySidebarHUD()
        {
            var players = PlayerRegistry.AllPlayers;
            float cardWidth = 215f;
            float cardHeight = 52f;
            float spacing = 6f;
            float startX = Screen.width - cardWidth - 14f;
            float startY = 140f;

            int drawnIndex = 0;
            for (int i = 0; i < players.Count; i++)
            {
                PlayerController p = players[i];
                if (p == null) continue;

                Rect cardRect = new Rect(startX, startY + drawnIndex * (cardHeight + spacing), cardWidth, cardHeight);
                drawnIndex++;

                Color slotColor = SlotColors[Mathf.Abs(p.PartySlotIndex) % SlotColors.Length];
                Health h = p.PlayerHealth ?? p.GetComponent<Health>();
                float curHp = h != null ? h.CurrentHealth : 100f;
                float maxHp = h != null && h.MaxHealth > 0f ? h.MaxHealth : 100f;
                float hpRatio = Mathf.Clamp01(curHp / maxHp);

                // พื้นหลังการ์ด
                GUI.color = new Color(0.07f, 0.09f, 0.13f, 0.88f);
                GUI.DrawTexture(cardRect, _whiteTex);

                // แถบสีประจำตัวด้านซ้าย
                GUI.color = slotColor;
                GUI.DrawTexture(new Rect(cardRect.x, cardRect.y, 5f, cardRect.height), _whiteTex);
                GUI.color = Color.white;

                string classId = p.ActiveCharacterData != null ? p.ActiveCharacterData.characterId : "SURVIVOR";
                string displayName = string.IsNullOrEmpty(p.NetworkPlayerName)
                    ? (p.IsRemotePlayer ? $"ALLY-{p.PartySlotIndex + 1}" : "YOU (P1)")
                    : p.NetworkPlayerName;
                int pStageX = !p.IsRemotePlayer && WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : p.NetworkStageX;
                int pStageY = !p.IsRemotePlayer && WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : p.NetworkStageY;

                GUI.Label(new Rect(cardRect.x + 12f, cardRect.y + 4f, cardRect.width - 18f, 18f), $"P{p.PartySlotIndex + 1} • {displayName} [{classId}] ({pStageX},{pStageY})", _nameStyle);

                // หลอดเลือดหรือหลอด Bleedout ตอนล้ม
                Rect barBg = new Rect(cardRect.x + 12f, cardRect.y + 25f, cardRect.width - 22f, 10f);
                GUI.color = new Color(0.18f, 0.20f, 0.25f, 1f);
                GUI.DrawTexture(barBg, _whiteTex);

                if (p.IsDowned)
                {
                    float bleedRatio = Mathf.Clamp01(p.DownedBleedoutTimer / PlayerController.MaxDownedBleedoutSeconds);
                    GUI.color = new Color(0.95f, 0.25f, 0.20f, 1f);
                    GUI.DrawTexture(new Rect(barBg.x, barBg.y, barBg.width * bleedRatio, barBg.height), _whiteTex);

                    if (p.ReviveProgress01 > 0.01f)
                    {
                        GUI.color = new Color(0.20f, 0.95f, 0.45f, 1f);
                        GUI.DrawTexture(new Rect(barBg.x, barBg.y, barBg.width * p.ReviveProgress01, barBg.height), _whiteTex);
                    }

                    GUI.color = Color.white;
                    _statusStyle.normal.textColor = new Color(1f, 0.55f, 0.20f);
                    GUI.Label(new Rect(cardRect.x + 12f, cardRect.y + 35f, cardRect.width - 16f, 16f),
                        $"⚠️ DOWNED ({p.DownedBleedoutTimer:F0}s) — Hold [E] Revive", _statusStyle);
                }
                else
                {
                    GUI.color = hpRatio > 0.35f ? new Color(0.22f, 0.85f, 0.42f, 1f) : new Color(0.95f, 0.30f, 0.25f, 1f);
                    GUI.DrawTexture(new Rect(barBg.x, barBg.y, barBg.width * hpRatio, barBg.height), _whiteTex);
                    GUI.color = Color.white;

                    _statusStyle.normal.textColor = new Color(0.78f, 0.84f, 0.92f);
                    string wepName = p.CurrentHeldItemType == PlayerController.HeldItemType.Melee
                        ? p.CurrentMeleeWeaponName
                        : p.CurrentWeaponName;
                    GUI.Label(new Rect(cardRect.x + 12f, cardRect.y + 35f, cardRect.width - 16f, 16f),
                        $"HP: {curHp:F0}/{maxHp:F0}   •   {(string.IsNullOrEmpty(wepName) ? "Unarmed" : wepName)}", _statusStyle);
                }
            }
        }

        private void DrawTeammateOverheadWorldTags()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            var players = PlayerRegistry.AllPlayers;
            for (int i = 0; i < players.Count; i++)
            {
                PlayerController p = players[i];
                if (p == null || !p.IsRemotePlayer || !p.gameObject.activeInHierarchy || !p.IsInSameStageAsLocalPlayer) continue;
                // ถ้าพรีแฟบตัวละครมีป้ายชื่อ TMP_Text บนหัว (overheadNameText) อยู่แล้ว ไม่ต้องวาด OnGUI ซ้อนอีกชั้น
                if (p.HasOverheadNameLabel) continue;

                Vector3 worldHead = p.transform.position + Vector3.up * 2.15f;
                Vector3 screenPos = cam.WorldToScreenPoint(worldHead);
                if (screenPos.z <= 0f) continue;

                float guiX = screenPos.x;
                float guiY = Screen.height - screenPos.y;

                Color slotColor = SlotColors[Mathf.Abs(p.PartySlotIndex) % SlotColors.Length];
                string tagText = p.IsDowned
                    ? $"⚠️ {p.NetworkPlayerName} [HOLD E TO REVIVE {(p.ReviveProgress01 * 100f):F0}%]"
                    : $"P{p.PartySlotIndex + 1} • {p.NetworkPlayerName}";

                Rect labelRect = new Rect(guiX - 110f, guiY - 22f, 220f, 20f);
                _worldTagStyle.normal.textColor = p.IsDowned ? new Color(1f, 0.45f, 0.20f) : slotColor;
                GUI.Label(labelRect, tagText, _worldTagStyle);
            }
        }

        private void DrawPartyPortalCountdownBanner()
        {
            if (NetworkStageSyncManager.Instance == null || !NetworkStageSyncManager.Instance.IsPortalCountdownActive)
            {
                return;
            }

            float rem = NetworkStageSyncManager.Instance.PortalCountdownRemaining;
            var dir = NetworkStageSyncManager.Instance.PendingDirection;
            Rect bannerRect = new Rect((Screen.width - 460f) * 0.5f, 56f, 460f, 42f);

            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.08f, 0.12f, 0.20f, 0.94f);
            GUI.Box(bannerRect, $"🚪 PARTY TRAVEL TO {dir.ToString().ToUpper()} STAGE IN {rem:F1}s...", _bannerStyle);
            GUI.backgroundColor = prev;
        }
    }
}

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheLastEmpire.Network
{
    /// <summary>
    /// MultiplayerLobbyUI — สคริปต์ควบคุมหน้าจอ RoomPanel & Lobby ในซีน `Lobby_NetCode`
    /// ออกแบบให้ลาก Reference ทุกช่องใส่ใน Inspector เอง 100% (ไม่เช็กจากชื่อ GameObject เปลี่ยนชื่อได้อิสระ)
    /// </summary>
    public class MultiplayerLobbyUI : MonoBehaviour
    {
        public static MultiplayerLobbyUI Instance { get; private set; }
        public static Action OnRequestLaunchSecondClientWindow;

        [Header("1. Main Panels (ลาก Panel หลักมาใส่)")]
        [Tooltip("ลากหน้าต่างรายการห้อง (เช่น RoomPanel) มาใส่")]
        [SerializeField] private GameObject roomPanel;

        [Tooltip("ลากหน้าต่างเลือกตัวละครหลังเข้าห้อง (เช่น Lobby) มาใส่")]
        [SerializeField] private GameObject lobbyCharacterSelectPanel;

        [Header("2. RoomPanel — Action Buttons & Search")]
        [Tooltip("ปุ่มสร้างห้อง (เช่น EmbarkButton / CREATE)")]
        [SerializeField] private Button createRoomButton;

        [Tooltip("ปุ่มประตูออกกลับหน้าเมนูหลัก (เช่น Close มุมซ้ายล่าง)")]
        [SerializeField] private Button closeRoomPanelButton;

        [Tooltip("ช่องค้นหาชื่อห้อง / ตั้งชื่อห้อง / กรอกรหัสห้อง (เช่น SearchInputField)")]
        [SerializeField] private TMP_InputField searchInputField;

        [Tooltip("ปุ่มลูกศรเรียงลำดับบน TopBar ตัวที่ 1 (Optional)")]
        [SerializeField] private Button sortButtonA;

        [Tooltip("ปุ่มลูกศรเรียงลำดับบน TopBar ตัวที่ 2 (Optional)")]
        [SerializeField] private Button sortButtonB;

        [Header("3. RoomPanel — Pagination (BottomBar)")]
        [Tooltip("ปุ่มย้อนหน้าก่อนหน้า < (เช่น BottomBar/PreButton)")]
        [SerializeField] private Button prevPageButton;

        [Tooltip("ปุ่มไปหน้าถัดไป > (เช่น BottomBar/NextButton)")]
        [SerializeField] private Button nextPageButton;

        [Tooltip("ข้อความแสดงเลขหน้า เช่น 1/1 (เช่น BottomBar/Text (TMP))")]
        [SerializeField] private TMP_Text pageNumberText;

        [Header("4. RoomPanel — Room Rows (ลากแถวที่มี RoomThumbnailUI มาใส่)")]
        [Tooltip("รายการแถวห้องทั้ง 5 แถว (แปะ RoomThumbnailUI ที่แต่ละแถวแล้วลากมาใส่ลิสต์นี้)")]
        [SerializeField] private List<RoomThumbnailUI> roomSlots = new List<RoomThumbnailUI>();

        [Header("5. Lobby Character Select — Room Info & Buttons")]
        [Tooltip("ปุ่ม BACK ในหน้าเลือกตัวละคร Lobby เพื่อกดออกจากห้องกลับมาหน้า RoomPanel")]
        [SerializeField] private Button lobbyBackButton;

        [Tooltip("ข้อความแสดงชื่อห้อง รหัสห้อง และจำนวนผู้เล่นในหน้า Lobby (เช่น ModalSubtitle)")]
        [SerializeField] private TMP_Text lobbyRoomStatusText;

        [Tooltip("ข้อความบนปุ่มเริ่มเกมในหน้า Lobby (เช่น EmbarkButton/Label -> ENTER WASTELAND)")]
        [SerializeField] private TMP_Text lobbyEmbarkButtonLabel;

        [Header("6. Scene Navigation Settings")]
        [Tooltip("ชื่อซีนเมนูหลักเมื่อกดปุ่มประตูออก (Close)")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Tooltip("เปิด/ปิดการแสดงปุ่มลัด [F2] มุมขวาบนของจอ")]
        [SerializeField] private bool showTopRightF2ShortcutButton = false;

        private int _currentPage = 0;
        private int _totalPages = 1;
        private bool _sortDescending = false;
        private float _uiRefreshTimer = 0f;

        // [F2] Debug Overlay state
        private bool _isWindowOpen = false;
        private Rect _windowRect = new Rect(0, 0, 480, 490);
        private string _joinCodeInput = "127.0.0.1:7777";
        private string _statusMessage = "Ready — Play Solo Offline or Host/Join 1–6 Player LAN Co-op.";

        private GUIStyle _windowStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _subHeaderStyle;
        private GUIStyle _codeBoxStyle;
        private GUIStyle _btnStyle;
        private GUIStyle _badgeBtnStyle;
        private bool _stylesInitialized = false;

        public bool IsOpen => _isWindowOpen;
        public bool HasBoundRoomPanel => roomPanel != null;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                // หากตัวนี้เป็น Component ที่คุณวางไว้ในซีนเอง (มี roomPanel หรือไม่ได้อยู่บน Bootstrap) ให้ยึดตัวที่คุณตั้งค่าใน Inspector เป็นหลักเสมอ
                if (roomPanel != null || gameObject.name.IndexOf("EmpireNetworkBootstrap", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    MultiplayerLobbyUI old = Instance;
                    Instance = this;
                    if (old != null && old.gameObject.name.IndexOf("EmpireNetworkBootstrap", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Destroy(old);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            _windowRect.x = (Screen.width - _windowRect.width) * 0.5f;
            _windowRect.y = (Screen.height - _windowRect.height) * 0.5f;

            BindSerializedUIEvents();

            if (roomPanel != null && !EmpireNetworkManager.IsOnline)
            {
                roomPanel.SetActive(true);
                if (lobbyCharacterSelectPanel != null)
                {
                    lobbyCharacterSelectPanel.SetActive(false);
                }
            }

            RefreshSceneLobbyUI();
        }

        private void Update()
        {
            if (roomPanel != null || lobbyCharacterSelectPanel != null)
            {
                _uiRefreshTimer -= Time.unscaledDeltaTime;
                if (_uiRefreshTimer <= 0f)
                {
                    _uiRefreshTimer = 0.25f;
                    RefreshSceneLobbyUI();
                }
            }

            if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
            {
                ToggleLobbyWindow();
            }
        }

        // =====================================================
        //  1. Inspector Reference Event Binding (100% Reference-based)
        // =====================================================

        private void BindSerializedUIEvents()
        {
            if (createRoomButton != null)
            {
                createRoomButton.onClick.RemoveListener(OnCreateRoomClicked);
                createRoomButton.onClick.AddListener(OnCreateRoomClicked);
            }

            if (closeRoomPanelButton != null)
            {
                closeRoomPanelButton.onClick.RemoveListener(OnCloseRoomPanelClicked);
                closeRoomPanelButton.onClick.AddListener(OnCloseRoomPanelClicked);
            }

            if (searchInputField != null)
            {
                searchInputField.onValueChanged.RemoveListener(OnSearchInputChanged);
                searchInputField.onValueChanged.AddListener(OnSearchInputChanged);
            }

            if (sortButtonA != null)
            {
                sortButtonA.onClick.RemoveListener(OnToggleSortClicked);
                sortButtonA.onClick.AddListener(OnToggleSortClicked);
            }

            if (sortButtonB != null)
            {
                sortButtonB.onClick.RemoveListener(OnToggleSortClicked);
                sortButtonB.onClick.AddListener(OnToggleSortClicked);
            }

            if (prevPageButton != null)
            {
                prevPageButton.onClick.RemoveListener(OnPrevPageClicked);
                prevPageButton.onClick.AddListener(OnPrevPageClicked);
            }

            if (nextPageButton != null)
            {
                nextPageButton.onClick.RemoveListener(OnNextPageClicked);
                nextPageButton.onClick.AddListener(OnNextPageClicked);
            }

            if (lobbyBackButton != null)
            {
                lobbyBackButton.onClick.RemoveListener(OnLobbyBackClicked);
                lobbyBackButton.onClick.AddListener(OnLobbyBackClicked);
            }
        }

        public void OnCreateRoomClicked()
        {
            if (EmpireNetworkManager.Instance == null) return;

            string customName = GetCleanSearchQuery();
            EmpireNetworkManager.Instance.StartHostSession(LobbyRelayService.DefaultUdpPort, customName);

            if (roomPanel != null) roomPanel.SetActive(false);
            if (lobbyCharacterSelectPanel != null) lobbyCharacterSelectPanel.SetActive(true);
            RefreshSceneLobbyUI();
        }

        public void OnJoinRoomClicked(string targetEndpoint, string roomDisplayName)
        {
            if (EmpireNetworkManager.Instance == null) return;

            if (EmpireNetworkManager.Instance.StartClientSession(targetEndpoint))
            {
                _statusMessage = $"Joined {roomDisplayName} ({targetEndpoint})!";
                if (roomPanel != null) roomPanel.SetActive(false);
                if (lobbyCharacterSelectPanel != null) lobbyCharacterSelectPanel.SetActive(true);
                RefreshSceneLobbyUI();
            }
        }

        public void OnLobbyBackClicked()
        {
            TryReturnToRoomPanel();
        }

        public bool TryReturnToRoomPanel()
        {
            if (roomPanel == null) return false;
            EmpireNetworkManager.Instance?.DisconnectSession();
            if (lobbyCharacterSelectPanel != null) lobbyCharacterSelectPanel.SetActive(false);
            roomPanel.SetActive(true);
            RefreshSceneLobbyUI();
            return true;
        }

        public void OnCloseRoomPanelClicked()
        {
            EmpireNetworkManager.Instance?.DisconnectSession();
            if (!string.IsNullOrEmpty(mainMenuSceneName) && SceneManager.GetActiveScene().name != mainMenuSceneName)
            {
                SceneManager.LoadScene(mainMenuSceneName);
            }
            else if (roomPanel != null)
            {
                roomPanel.SetActive(false);
            }
        }

        private void OnSearchInputChanged(string newText)
        {
            _currentPage = 0;
            RefreshSceneLobbyUI();
        }

        public void OnToggleSortClicked()
        {
            _sortDescending = !_sortDescending;
            RefreshSceneLobbyUI();
        }

        public void OnPrevPageClicked()
        {
            if (_currentPage > 0)
            {
                _currentPage--;
                RefreshSceneLobbyUI();
            }
        }

        public void OnNextPageClicked()
        {
            if (_currentPage < _totalPages - 1)
            {
                _currentPage++;
                RefreshSceneLobbyUI();
            }
        }

        private string GetCleanSearchQuery()
        {
            if (searchInputField == null || string.IsNullOrEmpty(searchInputField.text)) return string.Empty;
            return searchInputField.text.Replace("\u200B", "").Trim();
        }

        private void RefreshSceneLobbyUI()
        {
            // 1. อัปเดตข้อความสถานะห้องในหน้า Lobby เลือกตัวละคร
            if (lobbyCharacterSelectPanel != null && lobbyCharacterSelectPanel.activeSelf && EmpireNetworkManager.Instance != null)
            {
                int partyCount = EmpireNetworkManager.Instance.ConnectedPeerCount;
                if (lobbyRoomStatusText != null)
                {
                    string roleTag = EmpireNetworkManager.IsOnlineHost ? "HOSTING LAN ROOM" : "CONNECTED TO LAN ROOM";
                    lobbyRoomStatusText.text =
                        $"<color=#FACC15>{roleTag}: {EmpireNetworkManager.Instance.ActiveRoomName}</color>   •   " +
                        $"CODE: <color=#38BDF8>{EmpireNetworkManager.Instance.ActiveJoinCode}</color> ({EmpireNetworkManager.Instance.ActiveHostIp}:{EmpireNetworkManager.Instance.ActivePort})   •   " +
                        $"PARTY: <color=#4ADE80>{partyCount}/6 PLAYERS</color>";
                }

                if (lobbyEmbarkButtonLabel != null)
                {
                    lobbyEmbarkButtonLabel.text = EmpireNetworkManager.IsOnlineHost
                        ? $"ENTER WASTELAND ({partyCount}/6)"
                        : "ENTER WASTELAND (READY)";
                }
            }

            if (roomPanel == null || !roomPanel.activeSelf) return;
            if (roomSlots == null || roomSlots.Count == 0) return;

            string query = GetCleanSearchQuery();
            List<DiscoveredLanRoom> discovered = LobbyRelayService.GetActiveLanRooms();

            if (!string.IsNullOrEmpty(query))
            {
                for (int i = discovered.Count - 1; i >= 0; i--)
                {
                    var r = discovered[i];
                    bool match = (r.roomName != null && r.roomName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                 (r.joinCode != null && r.joinCode.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                 (r.hostIp != null && r.hostIp.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (!match) discovered.RemoveAt(i);
                }
            }

            discovered.Sort((a, b) => _sortDescending
                ? string.Compare(b.roomName, a.roomName, StringComparison.OrdinalIgnoreCase)
                : string.Compare(a.roomName, b.roomName, StringComparison.OrdinalIgnoreCase));

            int slotsPerPage = Mathf.Max(1, roomSlots.Count);
            _totalPages = Mathf.Max(1, Mathf.CeilToInt(discovered.Count / (float)slotsPerPage));
            _currentPage = Mathf.Clamp(_currentPage, 0, _totalPages - 1);

            if (pageNumberText != null)
            {
                pageNumberText.text = $"{_currentPage + 1}/{_totalPages}";
            }
            if (prevPageButton != null) prevPageButton.interactable = _currentPage > 0;
            if (nextPageButton != null) nextPageButton.interactable = _currentPage < _totalPages - 1;

            if (discovered.Count > 0)
            {
                int startIndex = _currentPage * slotsPerPage;
                for (int i = 0; i < roomSlots.Count; i++)
                {
                    RoomThumbnailUI slot = roomSlots[i];
                    if (slot == null) continue;

                    int dataIndex = startIndex + i;
                    if (dataIndex < discovered.Count)
                    {
                        DiscoveredLanRoom room = discovered[dataIndex];
                        string displayTitle = $"{room.roomName}   <color=#38BDF8>[{room.joinCode}]</color>   <size=80%><color=#94A3B8>({room.hostIp})</color></size>";
                        string countStr = $"{room.currentPlayers}/{room.maxPlayers}";
                        bool canJoin = room.currentPlayers < room.maxPlayers;
                        string endpoint = $"{room.hostIp}:{room.hostPort}";
                        string rName = room.roomName;

                        slot.SetRoomData(displayTitle, countStr, canJoin, "Join", () => OnJoinRoomClicked(endpoint, rName));
                    }
                    else
                    {
                        slot.HideRow();
                    }
                }
            }
            else
            {
                for (int i = 0; i < roomSlots.Count; i++)
                {
                    RoomThumbnailUI slot = roomSlots[i];
                    if (slot == null) continue;

                    if (i == 0)
                    {
                        bool hasDirectQuery = !string.IsNullOrEmpty(query);
                        if (hasDirectQuery)
                        {
                            string target = query;
                            slot.SetRoomData(
                                $"DIRECT JOIN / CREATE ROOM:  <color=#FACC15>{query}</color>",
                                "1/6",
                                true,
                                "Join",
                                () => OnJoinRoomClicked(target, target));
                        }
                        else
                        {
                            slot.SetRoomData(
                                "<color=#94A3B8>NO LAN ROOMS FOUND — CLICK [CREATE] TO HOST A ROOM</color>",
                                "0/6",
                                false,
                                "Wait",
                                null);
                        }
                    }
                    else
                    {
                        slot.HideRow();
                    }
                }
            }
        }

        // =====================================================
        //  2. [F2] In-Game Co-op Quick Overlay & Test Menu
        // =====================================================

        public void ToggleLobbyWindow()
        {
            _isWindowOpen = !_isWindowOpen;
            if (_isWindowOpen)
            {
                _windowRect.x = (Screen.width - _windowRect.width) * 0.5f;
                _windowRect.y = (Screen.height - _windowRect.height) * 0.5f;
            }
        }

        private void EnsureStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            _windowStyle = new GUIStyle(GUI.skin.window)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _headerStyle.normal.textColor = new Color(0.22f, 0.74f, 0.97f);

            _subHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            _subHeaderStyle.normal.textColor = new Color(0.88f, 0.90f, 0.95f);

            _codeBoxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _codeBoxStyle.normal.textColor = new Color(0.98f, 0.80f, 0.15f);

            _btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };

            _badgeBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
        }

        private void OnGUI()
        {
            EnsureStyles();

            Color prevBg = GUI.backgroundColor;

            if (showTopRightF2ShortcutButton && roomPanel == null)
            {
                int partyCount = Mathf.Max(1, PlayerRegistry.GetTotalPlayerCount());
                string badgeLabel = EmpireNetworkManager.IsOnline
                    ? $"🌐 CO-OP ({partyCount}/6) [F2]"
                    : "🌐 CO-OP LOBBY [F2]";

                GUI.backgroundColor = EmpireNetworkManager.IsOnline
                    ? new Color(0.15f, 0.75f, 0.45f, 0.92f)
                    : new Color(0.18f, 0.24f, 0.34f, 0.88f);

                if (GUI.Button(new Rect(Screen.width - 175f, 12f, 162f, 30f), badgeLabel, _badgeBtnStyle))
                {
                    ToggleLobbyWindow();
                }
                GUI.backgroundColor = prevBg;
            }

            if (!_isWindowOpen) return;

            GUI.backgroundColor = new Color(0.08f, 0.10f, 0.15f, 0.97f);
            _windowRect = GUI.Window(94821, _windowRect, DrawLobbyWindowContents, "THE LAST EMPIRE — 1-6 PLAYER LAN CO-OP [F2]", _windowStyle);
            GUI.backgroundColor = prevBg;
        }

        private void DrawLobbyWindowContents(int windowId)
        {
            GUILayout.Space(8);
            int totalPlayers = EmpireNetworkManager.Instance != null ? EmpireNetworkManager.Instance.ConnectedPeerCount : Mathf.Max(1, PlayerRegistry.GetTotalPlayerCount());
            string modeLabel = !EmpireNetworkManager.IsOnline
                ? "OFFLINE SINGLEPLAYER"
                : (EmpireNetworkManager.IsOnlineHost ? $"HOSTING LAN ROOM ({totalPlayers}/6 PLAYERS)" : $"CONNECTED AS LAN CLIENT ({totalPlayers}/6 PLAYERS)");

            GUILayout.Label($"Status: {modeLabel}", _headerStyle);
            GUILayout.Space(6);

            if (EmpireNetworkManager.IsOnline && EmpireNetworkManager.Instance != null)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("LAN ROOM CODE & ENDPOINT:", _subHeaderStyle);
                string codeDisplay = $"{EmpireNetworkManager.Instance.ActiveJoinCode}   ({EmpireNetworkManager.Instance.ActiveHostIp}:{EmpireNetworkManager.Instance.ActivePort})";
                GUILayout.Box(codeDisplay, _codeBoxStyle, GUILayout.Height(38));

                GUILayout.BeginHorizontal();
                GUI.backgroundColor = new Color(0.25f, 0.65f, 0.95f);
                if (GUILayout.Button("📋 Copy 6-Digit Code", _btnStyle, GUILayout.Height(28)))
                {
                    GUIUtility.systemCopyBuffer = EmpireNetworkManager.Instance.ActiveJoinCode;
                    _statusMessage = $"Copied Join Code '{EmpireNetworkManager.Instance.ActiveJoinCode}'!";
                }
                if (GUILayout.Button("📋 Copy IP:Port", _btnStyle, GUILayout.Height(28)))
                {
                    GUIUtility.systemCopyBuffer = $"{EmpireNetworkManager.Instance.ActiveHostIp}:{EmpireNetworkManager.Instance.ActivePort}";
                    _statusMessage = $"Copied '{GUIUtility.systemCopyBuffer}'!";
                }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.Space(6);
            }

            GUILayout.BeginVertical(GUI.skin.box);
            if (OnRequestLaunchSecondClientWindow != null)
            {
                GUI.backgroundColor = new Color(0.15f, 0.72f, 0.95f);
                if (GUILayout.Button("🖥️ LAUNCH 2ND CLIENT WINDOW (Test 2 Clients on 1 PC)", _btnStyle, GUILayout.Height(34)))
                {
                    OnRequestLaunchSecondClientWindow.Invoke();
                    _statusMessage = "Launching 2nd Client Window...";
                }
            }

            var lanRooms = LobbyRelayService.GetActiveLanRooms();
            if (lanRooms.Count > 0)
            {
                GUILayout.Label($"📡 DISCOVERED LAN ROOMS ({lanRooms.Count}):", _subHeaderStyle);
                for (int i = 0; i < lanRooms.Count; i++)
                {
                    var room = lanRooms[i];
                    GUI.backgroundColor = new Color(0.16f, 0.78f, 0.55f);
                    string roomBtnText = $"⚡ JOIN [{room.roomName}] ({room.hostIp}:{room.hostPort}) — {room.currentPlayers}/{room.maxPlayers}P";
                    if (GUILayout.Button(roomBtnText, _btnStyle, GUILayout.Height(30)))
                    {
                        _joinCodeInput = $"{room.hostIp}:{room.hostPort}";
                        EmpireNetworkManager.Instance?.StartClientSession(_joinCodeInput);
                    }
                }
            }
            GUILayout.EndVertical();

            GUILayout.Space(6);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("INSTANT IN-GAME PARTY TEST (SPAWN AI SQUADMATES 1–6P):", _subHeaderStyle);

            GUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.95f, 0.65f, 0.15f);
            if (GUILayout.Button($"➕ Spawn Co-op Teammate ({PlayerRegistry.GetTotalPlayerCount()}/6)", _btnStyle, GUILayout.Height(32)))
            {
                PlayerController bot = EmpireNetworkManager.Instance?.SpawnSimulatedCoopTeammate();
                _statusMessage = bot != null
                    ? $"Spawned {bot.NetworkPlayerName} ({bot.ActiveCharacterData?.characterId})!"
                    : "Enter Gameplay_3D first to spawn in-game teammates.";
            }

            GUI.backgroundColor = new Color(0.75f, 0.35f, 0.25f);
            if (GUILayout.Button("✖ Clear Bots", _btnStyle, GUILayout.Height(32), GUILayout.Width(110)))
            {
                EmpireNetworkManager.Instance?.ClearSimulatedTeammates();
            }
            GUILayout.EndHorizontal();

            GUI.backgroundColor = new Color(0.65f, 0.45f, 0.85f);
            if (GUILayout.Button("🩹 Test Downed State on First Teammate (Hold [E] to Revive)", _btnStyle, GUILayout.Height(28)))
            {
                var all = PlayerRegistry.AllPlayers;
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i] != null && all[i].IsRemotePlayer && !all[i].IsDowned)
                    {
                        Health h = all[i].PlayerHealth ?? all[i].GetComponent<Health>();
                        if (h != null) h.TakeDamage(h.CurrentHealth + 999f, all[i].transform.position, false);
                        break;
                    }
                }
            }
            GUILayout.EndVertical();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (EmpireNetworkManager.IsOnline)
            {
                GUI.backgroundColor = new Color(0.88f, 0.25f, 0.28f);
                if (GUILayout.Button("⏹ Disconnect Session", _btnStyle, GUILayout.Height(32)))
                {
                    EmpireNetworkManager.Instance?.DisconnectSession();
                }
            }
            GUI.backgroundColor = new Color(0.45f, 0.50f, 0.58f);
            if (GUILayout.Button("Close [F2]", _btnStyle, GUILayout.Height(32)))
            {
                _isWindowOpen = false;
            }
            GUILayout.EndHorizontal();

            GUI.backgroundColor = Color.white;
            GUILayout.Label(_statusMessage, _subHeaderStyle);
            GUI.DragWindow(new Rect(0, 0, 10000, 28));
        }
    }
}

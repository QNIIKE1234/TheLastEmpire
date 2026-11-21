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
    /// MultiplayerLobbyUI — ควบคุมทั้ง:
    /// 1. หน้าจอ Canvas UI ในซีนใหม่ `Lobby_NetCode.unity` (`RoomPanel` ค้นหา/สร้างห้อง LAN และ `Lobby` เลือกตัวละครก่อนกด `ENTER WASTELAND`)
    /// 2. หน้าต่างลัด [F2] สำหรับเสกบอทเพื่อนร่วมทีม / ทดสอบระหว่างอยู่ในด่าน
    /// </summary>
    public class MultiplayerLobbyUI : MonoBehaviour
    {
        public static MultiplayerLobbyUI Instance { get; private set; }
        public static Action OnRequestLaunchSecondClientWindow;

        [Serializable]
        public class RoomThumbnailSlot
        {
            public GameObject root;
            public TMP_Text roomNameText;
            public TMP_Text countText;
            public Image lockImage;
            public Button joinButton;
            public TMP_Text joinButtonLabel;
        }

        [Header("Lobby_NetCode Scene UI References (RoomPanel)")]
        [SerializeField] private GameObject _sceneRoomPanel;
        [SerializeField] private GameObject _sceneLobbyPanel;
        [SerializeField] private TMP_InputField _sceneSearchInput;
        [SerializeField] private Button _sceneCreateButton;
        [SerializeField] private Button _sceneCloseRoomPanelButton;
        [SerializeField] private Button _scenePrevPageButton;
        [SerializeField] private Button _sceneNextPageButton;
        [SerializeField] private TMP_Text _scenePageNumberText;
        [SerializeField] private List<RoomThumbnailSlot> _roomThumbnailSlots = new List<RoomThumbnailSlot>();

        [Header("Lobby_NetCode Scene UI References (Lobby Modal)")]
        [SerializeField] private TMP_Text _sceneLobbySubtitleText;
        [SerializeField] private TMP_Text _sceneLobbyEmbarkLabel;

        private bool _isSceneLobbyBound = false;
        private int _currentPage = 0;
        private int _totalPages = 1;
        private bool _sortDescending = false;
        private float _sceneUiRefreshTimer = 0f;

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
        private GUIStyle _textFieldStyle;
        private GUIStyle _badgeBtnStyle;
        private bool _stylesInitialized = false;

        public bool IsOpen => _isWindowOpen;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                // หากสคริปต์นี้แปะอยู่บน RoomPanel ในซีนโดยตรง ให้ยึดตัวบน RoomPanel เป็นหลัก
                if (gameObject.name.IndexOf("RoomPanel", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    MultiplayerLobbyUI old = Instance;
                    Instance = this;
                    if (old != null && old.gameObject.name.IndexOf("EmpireNetworkBootstrap", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Destroy(old);
                    }
                }
            }
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [ContextMenu("Auto-Bind RoomPanel UI References")]
        public void EditorAutoBindReferences()
        {
            TryBindLobbyNetcodeSceneUI();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            _windowRect.x = (Screen.width - _windowRect.width) * 0.5f;
            _windowRect.y = (Screen.height - _windowRect.height) * 0.5f;
            TryBindLobbyNetcodeSceneUI();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _isSceneLobbyBound = false;
            _roomThumbnailSlots.Clear();
            TryBindLobbyNetcodeSceneUI();
        }

        private void Update()
        {
            if (!_isSceneLobbyBound)
            {
                TryBindLobbyNetcodeSceneUI();
            }
            else
            {
                _sceneUiRefreshTimer -= Time.unscaledDeltaTime;
                if (_sceneUiRefreshTimer <= 0f)
                {
                    _sceneUiRefreshTimer = 0.25f;
                    RefreshSceneLobbyUI();
                }
            }

            if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
            {
                ToggleLobbyWindow();
            }
        }

        // =====================================================
        //  1. Scene-Native `Lobby_NetCode.unity` UI Binding
        // =====================================================

        private void TryBindLobbyNetcodeSceneUI()
        {
            GameObject canvasObj = GameObject.Find("MainMenuCanvas");
            if (canvasObj == null) return;

            Transform roomPanelTf = canvasObj.transform.Find("RoomPanel");
            if (roomPanelTf == null) return;

            _sceneRoomPanel = roomPanelTf.gameObject;
            Transform lobbyTf = canvasObj.transform.Find("Lobby");
            _sceneLobbyPanel = lobbyTf != null ? lobbyTf.gameObject : null;

            // 1. ปุ่ม CREATE (RoomPanel -> EmbarkButton)
            Transform createBtnTf = roomPanelTf.Find("EmbarkButton");
            if (createBtnTf != null)
            {
                _sceneCreateButton = createBtnTf.GetComponent<Button>();
                if (_sceneCreateButton != null)
                {
                    _sceneCreateButton.onClick.RemoveListener(OnSceneCreateRoomClicked);
                    _sceneCreateButton.onClick.AddListener(OnSceneCreateRoomClicked);
                }
            }

            // 2. ปุ่มประตูออกมุมซ้ายล่าง (RoomPanel -> Close)
            Transform closeBtnTf = roomPanelTf.Find("Close");
            if (closeBtnTf != null)
            {
                _sceneCloseRoomPanelButton = closeBtnTf.GetComponent<Button>();
                if (_sceneCloseRoomPanelButton != null)
                {
                    _sceneCloseRoomPanelButton.onClick.RemoveListener(OnSceneExitToMainMenuClicked);
                    _sceneCloseRoomPanelButton.onClick.AddListener(OnSceneExitToMainMenuClicked);
                }
            }

            // 3. ช่อง Search / ตั้งชื่อห้อง / กรอกรหัสห้อง (RoomPanel -> TopBar -> SearchInputField)
            Transform topBarTf = roomPanelTf.Find("TopBar");
            if (topBarTf != null)
            {
                Transform searchTf = topBarTf.Find("SearchInputField");
                if (searchTf != null)
                {
                    _sceneSearchInput = searchTf.GetComponent<TMP_InputField>();
                    if (_sceneSearchInput != null)
                    {
                        _sceneSearchInput.onValueChanged.RemoveListener(OnSceneSearchInputChanged);
                        _sceneSearchInput.onValueChanged.AddListener(OnSceneSearchInputChanged);
                    }
                }

                // ปุ่มลูกศรเรียงลำดับ / รีเฟรชห้องบน TopBar
                Button[] topButtons = topBarTf.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < topButtons.Length; i++)
                {
                    Button b = topButtons[i];
                    b.onClick.RemoveListener(OnSceneToggleSortClicked);
                    b.onClick.AddListener(OnSceneToggleSortClicked);
                }
            }

            // 4. แถบเปลี่ยนหน้า BottomBar (< 1/1 >)
            Transform bottomBarTf = roomPanelTf.Find("BottomBar");
            if (bottomBarTf != null)
            {
                Transform preTf = bottomBarTf.Find("PreButton");
                if (preTf != null)
                {
                    _scenePrevPageButton = preTf.GetComponent<Button>();
                    if (_scenePrevPageButton != null)
                    {
                        _scenePrevPageButton.onClick.RemoveListener(OnScenePrevPageClicked);
                        _scenePrevPageButton.onClick.AddListener(OnScenePrevPageClicked);
                    }
                }

                Transform nextTf = bottomBarTf.Find("NextButton");
                if (nextTf != null)
                {
                    _sceneNextPageButton = nextTf.GetComponent<Button>();
                    if (_sceneNextPageButton != null)
                    {
                        _sceneNextPageButton.onClick.RemoveListener(OnSceneNextPageClicked);
                        _sceneNextPageButton.onClick.AddListener(OnSceneNextPageClicked);
                    }
                }

                Transform pageTxtTf = bottomBarTf.Find("Text (TMP)");
                if (pageTxtTf != null)
                {
                    _scenePageNumberText = pageTxtTf.GetComponent<TMP_Text>();
                }
            }

            // 5. รายการ 5 แถวห้องใน RoomsListPanel -> RoomsList
            _roomThumbnailSlots.Clear();
            Transform roomsListTf = roomPanelTf.Find("RoomsListPanel/RoomsList");
            if (roomsListTf != null)
            {
                for (int i = 0; i < roomsListTf.childCount; i++)
                {
                    Transform rowTf = roomsListTf.GetChild(i);
                    if (rowTf == null) continue;

                    Transform joinTf = rowTf.Find("JoinButton");
                    Button joinBtn = joinTf != null ? joinTf.GetComponent<Button>() : null;
                    TMP_Text joinLbl = joinTf != null ? joinTf.GetComponentInChildren<TMP_Text>(true) : null;

                    _roomThumbnailSlots.Add(new RoomThumbnailSlot
                    {
                        root = rowTf.gameObject,
                        roomNameText = rowTf.Find("RoomNameText")?.GetComponent<TMP_Text>(),
                        countText = rowTf.Find("ConutText")?.GetComponent<TMP_Text>(),
                        lockImage = rowTf.Find("LockImage")?.GetComponent<Image>(),
                        joinButton = joinBtn,
                        joinButtonLabel = joinLbl
                    });
                }
            }

            // 6. อ้างอิงข้อความสถานะห้องในหน้าเลือกตัวละคร (Lobby -> ModalPanel -> ModalSubtitle / EmbarkButton)
            if (_sceneLobbyPanel != null)
            {
                Transform modalPanelTf = _sceneLobbyPanel.transform.Find("ModalPanel");
                if (modalPanelTf != null)
                {
                    _sceneLobbySubtitleText = modalPanelTf.Find("ModalSubtitle")?.GetComponent<TMP_Text>();
                    Transform lobbyEmbarkTf = modalPanelTf.Find("EmbarkButton/Label");
                    if (lobbyEmbarkTf != null)
                    {
                        _sceneLobbyEmbarkLabel = lobbyEmbarkTf.GetComponent<TMP_Text>();
                    }
                }
            }

            // ตั้งสถานะเริ่มต้นเมื่อเข้าซีน Lobby_NetCode ตอนรันเกม: เปิด RoomPanel และซ่อน Lobby (ยกเว้นเชื่อมต่อห้องอยู่แล้ว)
            if (Application.isPlaying)
            {
                if (!EmpireNetworkManager.IsOnline)
                {
                    _sceneRoomPanel.SetActive(true);
                    if (_sceneLobbyPanel != null) _sceneLobbyPanel.SetActive(false);
                }

                _isSceneLobbyBound = true;
                RefreshSceneLobbyUI();
                Debug.Log("<color=#38BDF8>[MultiplayerLobbyUI]</color> Bound Lobby_NetCode scene UI (RoomPanel + 5 RoomThumbnails + Lobby Operative Selection).");
            }
        }

        private void OnSceneCreateRoomClicked()
        {
            if (EmpireNetworkManager.Instance == null) return;

            string customName = GetCleanSearchQuery();
            EmpireNetworkManager.Instance.StartHostSession(LobbyRelayService.DefaultUdpPort, customName);

            // สลับจากหน้า RoomPanel ไปหน้า Lobby เลือกตัวละครทันที
            if (_sceneRoomPanel != null) _sceneRoomPanel.SetActive(false);
            if (_sceneLobbyPanel != null) _sceneLobbyPanel.SetActive(true);
            RefreshSceneLobbyUI();
        }

        private void OnSceneJoinRoomClicked(string targetEndpoint, string roomDisplayName)
        {
            if (EmpireNetworkManager.Instance == null) return;

            if (EmpireNetworkManager.Instance.StartClientSession(targetEndpoint))
            {
                _statusMessage = $"Joined {roomDisplayName} ({targetEndpoint})!";
                if (_sceneRoomPanel != null) _sceneRoomPanel.SetActive(false);
                if (_sceneLobbyPanel != null) _sceneLobbyPanel.SetActive(true);
                RefreshSceneLobbyUI();
            }
        }

        private void OnSceneExitToMainMenuClicked()
        {
            EmpireNetworkManager.Instance?.DisconnectSession();
            if (SceneManager.GetActiveScene().name != "MainMenu")
            {
                SceneManager.LoadScene("MainMenu");
            }
            else if (_sceneRoomPanel != null)
            {
                _sceneRoomPanel.SetActive(false);
            }
        }

        private void OnSceneSearchInputChanged(string newText)
        {
            _currentPage = 0;
            RefreshSceneLobbyUI();
        }

        private void OnSceneToggleSortClicked()
        {
            _sortDescending = !_sortDescending;
            RefreshSceneLobbyUI();
        }

        private void OnScenePrevPageClicked()
        {
            if (_currentPage > 0)
            {
                _currentPage--;
                RefreshSceneLobbyUI();
            }
        }

        private void OnSceneNextPageClicked()
        {
            if (_currentPage < _totalPages - 1)
            {
                _currentPage++;
                RefreshSceneLobbyUI();
            }
        }

        private string GetCleanSearchQuery()
        {
            if (_sceneSearchInput == null || string.IsNullOrEmpty(_sceneSearchInput.text)) return string.Empty;
            return _sceneSearchInput.text.Replace("\u200B", "").Trim();
        }

        private void RefreshSceneLobbyUI()
        {
            if (!_isSceneLobbyBound) return;

            // อัปเดตข้อความสถานะห้องบนหัวหน้าต่าง Lobby เลือกตัวละคร
            if (_sceneLobbyPanel != null && _sceneLobbyPanel.activeSelf && EmpireNetworkManager.Instance != null)
            {
                int partyCount = EmpireNetworkManager.Instance.ConnectedPeerCount;
                if (_sceneLobbySubtitleText != null)
                {
                    string roleTag = EmpireNetworkManager.IsOnlineHost ? "HOSTING LAN ROOM" : "CONNECTED TO LAN ROOM";
                    _sceneLobbySubtitleText.text =
                        $"<color=#FACC15>{roleTag}: {EmpireNetworkManager.Instance.ActiveRoomName}</color>   •   " +
                        $"CODE: <color=#38BDF8>{EmpireNetworkManager.Instance.ActiveJoinCode}</color> ({EmpireNetworkManager.Instance.ActiveHostIp}:{EmpireNetworkManager.Instance.ActivePort})   •   " +
                        $"PARTY: <color=#4ADE80>{partyCount}/6 PLAYERS</color>";
                }

                if (_sceneLobbyEmbarkLabel != null)
                {
                    _sceneLobbyEmbarkLabel.text = EmpireNetworkManager.IsOnlineHost
                        ? $"ENTER WASTELAND ({partyCount}/6)"
                        : "ENTER WASTELAND (READY)";
                }
            }

            if (_sceneRoomPanel == null || !_sceneRoomPanel.activeSelf) return;

            string query = GetCleanSearchQuery();
            List<DiscoveredLanRoom> discovered = LobbyRelayService.GetActiveLanRooms();

            // กรองตามคำค้นหาในช่อง SEARCH
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

            int slotsPerPage = Mathf.Max(1, _roomThumbnailSlots.Count);
            _totalPages = Mathf.Max(1, Mathf.CeilToInt(discovered.Count / (float)slotsPerPage));
            _currentPage = Mathf.Clamp(_currentPage, 0, _totalPages - 1);

            if (_scenePageNumberText != null)
            {
                _scenePageNumberText.text = $"{_currentPage + 1}/{_totalPages}";
            }
            if (_scenePrevPageButton != null) _scenePrevPageButton.interactable = _currentPage > 0;
            if (_sceneNextPageButton != null) _sceneNextPageButton.interactable = _currentPage < _totalPages - 1;

            // กรณีพบห้องในวง LAN 1 ห้องขึ้นไป -> แสดงรายการห้องจริง
            if (discovered.Count > 0)
            {
                int startIndex = _currentPage * slotsPerPage;
                for (int i = 0; i < _roomThumbnailSlots.Count; i++)
                {
                    RoomThumbnailSlot slot = _roomThumbnailSlots[i];
                    if (slot == null || slot.root == null) continue;

                    int dataIndex = startIndex + i;
                    if (dataIndex < discovered.Count)
                    {
                        DiscoveredLanRoom room = discovered[dataIndex];
                        slot.root.SetActive(true);

                        if (slot.roomNameText != null)
                        {
                            slot.roomNameText.text = $"{room.roomName}   <color=#38BDF8>[{room.joinCode}]</color>   <size=80%><color=#94A3B8>({room.hostIp})</color></size>";
                        }
                        if (slot.countText != null)
                        {
                            slot.countText.text = $"{room.currentPlayers}/{room.maxPlayers}";
                        }
                        if (slot.lockImage != null)
                        {
                            slot.lockImage.color = new Color(1f, 1f, 1f, 0.25f);
                        }
                        if (slot.joinButton != null)
                        {
                            slot.joinButton.gameObject.SetActive(true);
                            slot.joinButton.interactable = room.currentPlayers < room.maxPlayers;
                            string endpoint = $"{room.hostIp}:{room.hostPort}";
                            string rName = room.roomName;
                            slot.joinButton.onClick.RemoveAllListeners();
                            slot.joinButton.onClick.AddListener(() => OnSceneJoinRoomClicked(endpoint, rName));
                        }
                        if (slot.joinButtonLabel != null)
                        {
                            slot.joinButtonLabel.text = "Join";
                        }
                    }
                    else
                    {
                        slot.root.SetActive(false);
                    }
                }
            }
            else
            {
                // กรณียังไม่มีใครเปิดห้องในวง LAN (หรือพิมพ์รหัส 6 หลัก / IP:Port ในช่อง SEARCH เพื่อ Direct Join)
                for (int i = 0; i < _roomThumbnailSlots.Count; i++)
                {
                    RoomThumbnailSlot slot = _roomThumbnailSlots[i];
                    if (slot == null || slot.root == null) continue;

                    if (i == 0)
                    {
                        slot.root.SetActive(true);
                        bool hasDirectQuery = !string.IsNullOrEmpty(query);
                        if (hasDirectQuery)
                        {
                            if (slot.roomNameText != null)
                                slot.roomNameText.text = $"DIRECT JOIN / CREATE ROOM:  <color=#FACC15>{query}</color>";
                            if (slot.countText != null)
                                slot.countText.text = "1/6";
                            if (slot.joinButton != null)
                            {
                                slot.joinButton.gameObject.SetActive(true);
                                slot.joinButton.interactable = true;
                                string target = query;
                                slot.joinButton.onClick.RemoveAllListeners();
                                slot.joinButton.onClick.AddListener(() => OnSceneJoinRoomClicked(target, query));
                            }
                            if (slot.joinButtonLabel != null)
                                slot.joinButtonLabel.text = "Join";
                        }
                        else
                        {
                            if (slot.roomNameText != null)
                                slot.roomNameText.text = "<color=#94A3B8>NO LAN ROOMS FOUND — CLICK [CREATE] TO HOST A ROOM</color>";
                            if (slot.countText != null)
                                slot.countText.text = "0/6";
                            if (slot.joinButton != null)
                            {
                                slot.joinButton.interactable = false;
                            }
                            if (slot.joinButtonLabel != null)
                                slot.joinButtonLabel.text = "Wait";
                        }
                    }
                    else
                    {
                        slot.root.SetActive(false);
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

            _textFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
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

            // ซ่อนปุ่มมุมขวาบนเมื่ออยู่ในหน้าซีน Lobby_NetCode เพื่อให้ UI ใหม่สะอาดตา 100% (ยังกดปุ่มลัด F2 ได้ตามปกติ)
            if (!_isSceneLobbyBound)
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
            if (SceneManager.GetActiveScene().name != "Lobby_NetCode")
            {
                GUI.backgroundColor = new Color(0.96f, 0.72f, 0.12f);
                if (GUILayout.Button("🌐 OPEN CO-OP ROOM BROWSER SCENE (Lobby_NetCode)", _btnStyle, GUILayout.Height(34)))
                {
                    _isWindowOpen = false;
                    SceneManager.LoadScene("Lobby_NetCode");
                }
            }

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

            // ระบบเสกเพื่อนร่วมทีม Co-op จำลอง (Simulated Co-op Teammate Bot 1–6 คน)
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

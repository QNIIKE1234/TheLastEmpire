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
    /// MultiplayerLobbyUI — สคริปต์ควบคุมหน้าจอ `SelectRoomPanel` (ค้นหา/สร้างห้อง)
    /// และ `RoomInfoPanel` (หน้าห้องรอเริ่มเกม 6 คน พร้อมรูปโปรไฟล์ ชื่อ ปุ่มเตะคน [X] ปุ่ม LEAVE และปุ่ม ENTER WASTELAND)
    /// ออกแบบให้ลาก Reference ทุกช่องใส่ใน Inspector เอง 100% (ไม่เช็กจากชื่อ GameObject)
    /// </summary>
    public class MultiplayerLobbyUI : MonoBehaviour
    {
        public static MultiplayerLobbyUI Instance { get; private set; }
        public static Action OnRequestLaunchSecondClientWindow;

        [Header("1. Main Panels (ลากหน้าหลักมาใส่)")]
        [Tooltip("หน้าแรก: หน้าเลือกห้อง / สร้างห้อง (เช่น SelectRoomPanel)")]
        [SerializeField] private GameObject selectRoomPanel;

        [Tooltip("หน้าที่สอง: หน้าเตรียมพร้อมในห้อง 6 คนเมื่อกด CREATE หรือ Join (เช่น RoomInfoPanel)")]
        [SerializeField] private GameObject roomInfoPanel;

        [Tooltip("หน้าที่สาม: หน้าเลือกตัวละครที่เปิดขึ้นมาเมื่อเรากดปุ่มสีเหลืองรูปประแจบนการ์ดของตัวเอง (เช่น Lobby / CharacterSelectModal)")]
        [SerializeField] private GameObject characterSelectPanel;

        [Header("2. SelectRoomPanel — ปุ่มสร้างห้อง & ค้นหาห้อง")]
        [Tooltip("ปุ่ม CREATE มุมขวาล่างในหน้า SelectRoomPanel (เช่น EmbarkButton)")]
        [SerializeField] private Button createRoomButton;

        [Tooltip("ปุ่มประตูออกมุมซ้ายล่างในหน้า SelectRoomPanel (เช่น Close)")]
        [SerializeField] private Button closeSelectRoomButton;

        [Tooltip("ช่องค้นหาชื่อห้อง / ตั้งชื่อห้อง / กรอกรหัสห้อง (เช่น SearchInputField)")]
        [SerializeField] private TMP_InputField searchInputField;

        [Tooltip("ปุ่มลูกศรเรียงลำดับบน TopBar ตัวที่ 1 (Optional)")]
        [SerializeField] private Button sortButtonA;

        [Tooltip("ปุ่มลูกศรเรียงลำดับบน TopBar ตัวที่ 2 (Optional)")]
        [SerializeField] private Button sortButtonB;

        [Header("3. SelectRoomPanel — ปุ่มเปลี่ยนหน้า (BottomBar)")]
        [Tooltip("ปุ่มย้อนหน้าก่อนหน้า < (เช่น BottomBar/PreButton)")]
        [SerializeField] private Button prevPageButton;

        [Tooltip("ปุ่มไปหน้าถัดไป > (เช่น BottomBar/NextButton)")]
        [SerializeField] private Button nextPageButton;

        [Tooltip("ข้อความแสดงเลขหน้า เช่น 1/1 (เช่น BottomBar/Text (TMP))")]
        [SerializeField] private TMP_Text pageNumberText;

        [Header("4. SelectRoomPanel — รายการแถวห้อง (RoomThumbnailUI 5 แถว)")]
        [Tooltip("ลากแถว RoomThumbnail ทั้ง 5 แถว (ที่แปะสคริปต์ RoomThumbnailUI แล้ว) มาใส่ลิสต์นี้")]
        [SerializeField] private List<RoomThumbnailUI> roomSlots = new List<RoomThumbnailUI>();

        [Header("5. RoomInfoPanel — ปุ่ม LEAVE, ENTER WASTELAND (เฉพาะหัวห้อง) & หัวข้อห้อง")]
        [Tooltip("ปุ่มสีแดง LEAVE มุมซ้ายบนในหน้า RoomInfoPanel (เช่น ModalPanel/Close)")]
        [SerializeField] private Button leaveRoomButton;

        [Tooltip("ปุ่มสีเหลือง ENTER WASTELAND มุมขวาบนในหน้า RoomInfoPanel (หัวห้องกดเริ่มเกมได้คนเดียวเท่านั้น!)")]
        [SerializeField] private Button enterWastelandButton;

        [Tooltip("ถ้าติ๊กถูก เครื่องลูกทีม (Client) จะมองไม่เห็นปุ่ม ENTER WASTELAND (ซ่อนปุ่มไว้ให้หัวห้องเห็นและกดได้คนเดียว)")]
        [SerializeField] private bool hideStartButtonForNonHost = false;

        [Tooltip("ข้อความบนปุ่ม ENTER WASTELAND (Optional)")]
        [SerializeField] private TMP_Text enterWastelandButtonLabel;

        [Tooltip("ข้อความหัวข้อใหญ่ในหน้า RoomInfoPanel (เช่น ModalTitle — Optional)")]
        [SerializeField] private TMP_Text roomInfoTitleText;

        [Tooltip("ข้อความหัวข้อรองแสดงรหัสห้อง/จำนวนคนในหน้า RoomInfoPanel (เช่น ModalSubtitle — Optional)")]
        [SerializeField] private TMP_Text roomInfoSubtitleText;

        [Header("6. RoomInfoPanel — ช่องผู้เล่นทั้ง 6 คน & Character Database SO")]
        [Tooltip("ลาก MemberPrefabs ทั้ง 6 ช่อง (ที่แปะสคริปต์ RoomMemberSlotUI แล้ว) เรียงช่อง 1..6 มาใส่ลิสต์นี้")]
        [SerializeField] private List<RoomMemberSlotUI> memberSlots = new List<RoomMemberSlotUI>();

        [Tooltip("ลากไฟล์ CharacterDatabase.asset (ใน Assets/Resources_Addressable/Database/CharacterDatabase.asset) มาใส่ช่องเดียวจบ (ถ้าเว้นว่างจะโหลด CharacterDatabase.Instance ให้อัตโนมัติ)")]
        [SerializeField] private CharacterDatabase characterDatabase;

        [Tooltip("รูป Default กรณีผู้เล่นเพิ่งเข้าห้องแล้วยังไม่ได้กดปุ่มประแจเลือกตัวละคร (เช่น รูปเข็มทิศ)")]
        [SerializeField] private Sprite defaultUnselectedPortrait;

        [Header("7. Scene Navigation Settings")]
        [Tooltip("ชื่อซีนเมนูหลักเมื่อกดปุ่มประตูออกในหน้า SelectRoomPanel")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Tooltip("ชื่อซีนเกมเพลย์เมื่อกดปุ่ม ENTER WASTELAND ในหน้า RoomInfoPanel")]
        [SerializeField] private string gameplaySceneName = "Gameplay_3D";

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

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                if (selectRoomPanel != null || roomInfoPanel != null || gameObject.name.IndexOf("EmpireNetworkBootstrap", StringComparison.OrdinalIgnoreCase) < 0)
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

        private void OnEnable()
        {
            if (EmpireNetworkManager.Instance != null)
            {
                EmpireNetworkManager.Instance.OnLobbyRosterChanged += RefreshSceneLobbyUI;
                EmpireNetworkManager.Instance.OnKickedFromRoom += HandleKickedFromRoom;
            }
        }

        private void OnDisable()
        {
            if (EmpireNetworkManager.Instance != null)
            {
                EmpireNetworkManager.Instance.OnLobbyRosterChanged -= RefreshSceneLobbyUI;
                EmpireNetworkManager.Instance.OnKickedFromRoom -= HandleKickedFromRoom;
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

            if (EmpireNetworkManager.Instance != null)
            {
                EmpireNetworkManager.Instance.OnLobbyRosterChanged -= RefreshSceneLobbyUI;
                EmpireNetworkManager.Instance.OnLobbyRosterChanged += RefreshSceneLobbyUI;
                EmpireNetworkManager.Instance.OnKickedFromRoom -= HandleKickedFromRoom;
                EmpireNetworkManager.Instance.OnKickedFromRoom += HandleKickedFromRoom;
            }

            BindSerializedUIEvents();

            // ตอนเริ่มซีน: หากเชื่อมห้องอยู่แล้ว (เช่น กดรับ Steam Invite) ให้เปิด RoomInfoPanel เลย
            if (EmpireNetworkManager.IsOnline && roomInfoPanel != null)
            {
                if (selectRoomPanel != null) selectRoomPanel.SetActive(false);
                roomInfoPanel.SetActive(true);
                if (characterSelectPanel != null) characterSelectPanel.SetActive(false);
            }
            else if (selectRoomPanel != null)
            {
                selectRoomPanel.SetActive(true);
                if (roomInfoPanel != null) roomInfoPanel.SetActive(false);
                if (characterSelectPanel != null) characterSelectPanel.SetActive(false);
                EmpireNetworkManager.Instance?.RequestSteamLobbyRefresh(forceImmediate: true);
            }

            RefreshSceneLobbyUI();
        }

        private void Update()
        {
            if (selectRoomPanel != null || roomInfoPanel != null)
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

            if (closeSelectRoomButton != null)
            {
                closeSelectRoomButton.onClick.RemoveListener(OnCloseSelectRoomClicked);
                closeSelectRoomButton.onClick.AddListener(OnCloseSelectRoomClicked);
            }

            if (searchInputField != null)
            {
                searchInputField.onValueChanged.RemoveListener(OnSearchInputChanged);
                searchInputField.onValueChanged.AddListener(OnSearchInputChanged);
                searchInputField.onSubmit.RemoveListener(OnSearchInputSubmitted);
                searchInputField.onSubmit.AddListener(OnSearchInputSubmitted);
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

            if (leaveRoomButton != null)
            {
                leaveRoomButton.onClick.RemoveListener(OnLeaveRoomClicked);
                leaveRoomButton.onClick.AddListener(OnLeaveRoomClicked);
            }

            if (enterWastelandButton != null)
            {
                enterWastelandButton.onClick.RemoveListener(OnEnterWastelandClicked);
                enterWastelandButton.onClick.AddListener(OnEnterWastelandClicked);
            }
        }

        /// <summary>
        /// เมื่อกดปุ่ม CREATE ในหน้า SelectRoomPanel -> สร้างห้อง LAN Host และสลับเข้าหน้า RoomInfoPanel (6 คน)
        /// </summary>
        public void OnCreateRoomClicked()
        {
            if (EmpireNetworkManager.Instance == null) return;

            // ใช้ชื่อหัวห้อง (LocalPlayerName) เป็นชื่อห้องโดยอัตโนมัติ
            string hostRoomName = EmpireNetworkManager.GetOrInitLocalPlayerName();
            EmpireNetworkManager.Instance.StartHostSession(LobbyRelayService.DefaultUdpPort, hostRoomName);

            if (selectRoomPanel != null) selectRoomPanel.SetActive(false);
            if (roomInfoPanel != null) roomInfoPanel.SetActive(true);
            RefreshSceneLobbyUI();
        }

        /// <summary>
        /// เมื่อกดปุ่ม Join ในแถวห้อง -> จอยห้อง LAN Client และสลับเข้าหน้า RoomInfoPanel (6 คน)
        /// </summary>
        public void OnJoinRoomClicked(string targetEndpoint, string roomDisplayName)
        {
            if (EmpireNetworkManager.Instance == null) return;

            if (EmpireNetworkManager.Instance.StartClientSession(targetEndpoint))
            {
                _statusMessage = $"Joined {roomDisplayName} ({targetEndpoint})!";
                if (selectRoomPanel != null) selectRoomPanel.SetActive(false);
                if (roomInfoPanel != null) roomInfoPanel.SetActive(true);
                RefreshSceneLobbyUI();
            }
        }

        /// <summary>
        /// เมื่อกดปุ่มสีแดง LEAVE ในหน้า RoomInfoPanel -> ออกจากห้องและสลับกลับมาหน้า SelectRoomPanel
        /// </summary>
        public void OnLeaveRoomClicked()
        {
            TryReturnToRoomPanel();
        }

        public bool TryReturnToRoomPanel()
        {
            if (selectRoomPanel == null) return false;
            EmpireNetworkManager.Instance?.DisconnectSession();
            if (characterSelectPanel != null) characterSelectPanel.SetActive(false);
            if (roomInfoPanel != null) roomInfoPanel.SetActive(false);
            selectRoomPanel.SetActive(true);
            RefreshSceneLobbyUI();
            return true;
        }

        /// <summary>
        /// เรียกเมื่อผู้เล่นกดปุ่มสีเหลืองรูปประแจบนการ์ดของตัวเองในหน้า RoomInfoPanel เพื่อเปิดหน้าเลือกตัวละคร
        /// </summary>
        public void OpenLocalCharacterSelectFromRoomInfo()
        {
            if (characterSelectPanel == null) return;
            characterSelectPanel.SetActive(true);
        }

        /// <summary>
        /// เรียกเมื่อผู้เล่นเลือกตัวละครเสร็จในหน้า characterSelectPanel เพื่อซิงค์ตัวละครเข้าห้องและกลับไปหน้า RoomInfoPanel
        /// </summary>
        public bool CloseCharacterSelectBackToRoomInfo(string chosenCharacterId)
        {
            if (roomInfoPanel == null || characterSelectPanel == null || !characterSelectPanel.activeSelf)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(chosenCharacterId))
            {
                EmpireNetworkManager.Instance?.UpdateLocalLobbyCharacter(chosenCharacterId);
            }

            characterSelectPanel.SetActive(false);
            roomInfoPanel.SetActive(true);
            RefreshSceneLobbyUI();
            return true;
        }

        private void HandleKickedFromRoom()
        {
            _statusMessage = "You were removed from the room by the Host.";
            if (characterSelectPanel != null) characterSelectPanel.SetActive(false);
            if (roomInfoPanel != null) roomInfoPanel.SetActive(false);
            if (selectRoomPanel != null) selectRoomPanel.SetActive(true);
            RefreshSceneLobbyUI();
        }

        /// <summary>
        /// เมื่อกดปุ่มสีเหลือง ENTER WASTELAND ในหน้า RoomInfoPanel -> พาทุกคนในห้องเข้าด่าน Gameplay_3D
        /// </summary>
        public void OnEnterWastelandClicked()
        {
            if (EmpireNetworkManager.Instance != null)
            {
                // เฉพาะ Host (หรือกรณีออฟไลน์) ที่สั่งเริ่มเกมได้
                if (!EmpireNetworkManager.IsOnline || EmpireNetworkManager.IsOnlineHost)
                {
                    EmpireNetworkManager.Instance.HostStartGameplayFromLobby(gameplaySceneName);
                }
            }
            else
            {
                SceneManager.LoadScene(string.IsNullOrEmpty(gameplaySceneName) ? "Gameplay_3D" : gameplaySceneName);
            }
        }

        public void OnCloseSelectRoomClicked()
        {
            EmpireNetworkManager.Instance?.DisconnectSession();
            if (!string.IsNullOrEmpty(mainMenuSceneName) && SceneManager.GetActiveScene().name != mainMenuSceneName)
            {
                SceneManager.LoadScene(mainMenuSceneName);
            }
            else if (selectRoomPanel != null)
            {
                selectRoomPanel.SetActive(false);
            }
        }

        public void ShowRoomInfoPanelAfterExternalJoin()
        {
            if (selectRoomPanel != null) selectRoomPanel.SetActive(false);
            if (characterSelectPanel != null) characterSelectPanel.SetActive(false);
            if (roomInfoPanel != null) roomInfoPanel.SetActive(true);
            RefreshSceneLobbyUI();
        }

        private void OnSearchInputSubmitted(string submittedText)
        {
            string query = GetCleanSearchQuery();
            if (string.IsNullOrEmpty(query) || EmpireNetworkManager.IsOnline) return;

            // กด Enter หลังพิมพ์ Room Code 6 หลัก หรือ IP:Port เพื่อ Join ห้อง Steam Relay / LAN ได้ทันที
            if (query.Length == 6 || query.Contains(":") || query.Contains(".") || query.StartsWith("steam", StringComparison.OrdinalIgnoreCase))
            {
                OnJoinRoomClicked(query, query);
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
            if ( roomInfoPanel != null && roomInfoPanel.activeSelf)
            {
                RefreshRoomInfoPanelUI();
            }

            if (selectRoomPanel != null && selectRoomPanel.activeSelf)
            {
                RefreshSelectRoomPanelUI();
            }
        }

        // =====================================================
        //  RoomInfoPanel (6-Player Member Slots + Kick + Start)
        // =====================================================

        private void RefreshRoomInfoPanelUI()
        {
            if (EmpireNetworkManager.Instance == null) return;

            var members = EmpireNetworkManager.Instance.LobbyMembers;
            int partyCount = Mathf.Max(1, members.Count);
            bool isHost = EmpireNetworkManager.IsOnlineHost || !EmpireNetworkManager.IsOnline;
            ulong localClientId = EmpireNetworkManager.Instance.LocalClientId;

            if (roomInfoSubtitleText != null)
            {
                bool steamActive = EmpireNetworkManager.Instance.IsSteamRelayActive;
                string roleTag = isHost
                    ? (steamActive ? "HOSTING STEAM + LAN ROOM" : "HOSTING LAN ROOM")
                    : (steamActive ? "JOINED STEAM RELAY ROOM" : "JOINED LAN ROOM");
                string endpointInfo = steamActive
                    ? "STEAM RELAY ONLINE"
                    : $"{EmpireNetworkManager.Instance.ActiveHostIp}:{EmpireNetworkManager.Instance.ActivePort}";
                roomInfoSubtitleText.text =
                    $"<color=#FACC15>{roleTag}: {EmpireNetworkManager.Instance.ActiveRoomName}</color>  |  " +
                    $"CODE: <color=#38BDF8>{EmpireNetworkManager.Instance.ActiveJoinCode}</color> ({endpointInfo})  |  " +
                    $"PLAYERS: <color=#4ADE80>{partyCount}/6</color>";
            }

            // ปุ่มเริ่มเกม ENTER WASTELAND: หัวห้อง (Host) เท่านั้นที่กดเริ่มเกมได้!
            if (enterWastelandButton != null)
            {
                if (hideStartButtonForNonHost)
                {
                    enterWastelandButton.gameObject.SetActive(isHost);
                }
                else
                {
                    enterWastelandButton.gameObject.SetActive(true);
                    enterWastelandButton.interactable = isHost;
                }
            }

            if (enterWastelandButtonLabel != null)
            {
                enterWastelandButtonLabel.text = "ENTER WASTELAND";
            }

            if (memberSlots == null || memberSlots.Count == 0) return;

            for (int slotIdx = 0; slotIdx < memberSlots.Count; slotIdx++)
            {
                RoomMemberSlotUI slotUI = memberSlots[slotIdx];
                if (slotUI == null) continue;

                if (slotIdx < members.Count)
                {
                    LobbyMemberNetInfo info = members[slotIdx];
                    // หาก info.characterId ยังว่างอยู่ (ยังไม่ได้กดปุ่มประแจเลือกตัวละคร) จะคืนค่า null เพื่อให้ Slot แสดงรูป Default เข็มทิศ!
                    Sprite portrait = ResolvePortraitForCharacter(info.characterId);

                    // 1. ปุ่มเหลืองรูปประแจ: แสดงและกดได้เฉพาะช่องของตัวเราเองเท่านั้น!
                    bool isOurOwnSlot = isHost
                        ? (info.isHost || slotIdx == 0)
                        : (info.clientId == localClientId && !info.isHost);

                    // 2. ปุ่มแดง [X] เตะคน: แสดงเฉพาะบนหน้าจอของหัวห้อง (Host) บนช่องของลูกทีมคนอื่น
                    bool canHostKick = isHost && !isOurOwnSlot && !info.isHost;
                    int capturedSlot = info.slotIndex;

                    slotUI.SetOccupied(
                        info.playerName,
                        portrait,
                        defaultUnselectedPortrait,
                        isOurOwnSlot,
                        OpenLocalCharacterSelectFromRoomInfo,
                        canHostKick,
                        () => EmpireNetworkManager.Instance?.KickLobbyMemberBySlot(capturedSlot));
                }
                else
                {
                    slotUI.SetEmpty(slotIdx + 1, defaultUnselectedPortrait);
                }
            }
        }

        private Sprite ResolvePortraitForCharacter(string characterId)
        {
            // กรณีผู้เล่นเพิ่งเข้าห้องแล้วยังไม่ได้เลือกตัวละคร -> คืนค่า null เพื่อให้ใช้รูป Default (รูปเข็มทิศ)
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return null;
            }

            string id = characterId.Trim();
            CharacterDatabase db = characterDatabase != null ? characterDatabase : CharacterDatabase.Instance;
            if (db != null && db.Characters != null)
            {
                var list = db.Characters;
                for (int i = 0; i < list.Count; i++)
                {
                    CharacterData data = list[i];
                    if (data != null && string.Equals(data.characterId, id, StringComparison.OrdinalIgnoreCase))
                    {
                        if (data.characterPortrait != null)
                        {
                            return data.characterPortrait;
                        }
                        if (characterSelectPanel != null)
                        {
                            CharacterSelectUI csUi = characterSelectPanel.GetComponentInChildren<CharacterSelectUI>(true);
                            if (csUi != null)
                            {
                                Sprite fromCs = csUi.GetPortraitForCharacter(data);
                                if (fromCs != null) return fromCs;
                            }
                        }
                    }
                }
            }

            return null;
        }

        // =====================================================
        //  SelectRoomPanel (5 RoomThumbnail Rows + Search + Pagination)
        // =====================================================

        private void RefreshSelectRoomPanelUI()
        {
            if (roomSlots == null || roomSlots.Count == 0) return;

            EmpireNetworkManager.Instance?.RequestSteamLobbyRefresh();

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
                        string transportBadge = room.isSteamRelay ? "[STEAM] " : "[LAN] ";
                        string displayTitle = $"{transportBadge}{room.roomName}";
                        string countStr = $"{room.currentPlayers}/{room.maxPlayers}";
                        bool canJoin = room.currentPlayers < room.maxPlayers;
                        string endpoint = LobbyRelayService.GetJoinEndpoint(room);
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
                // ถ้ายังไม่มีใครสร้างห้องใน LAN เลย ให้ซ่อนแถวห้องทั้งหมด (ไม่แสดงแถวห้องหลอก 0/6 Wait)
                for (int i = 0; i < roomSlots.Count; i++)
                {
                    RoomThumbnailUI slot = roomSlots[i];
                    if (slot != null)
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

            if (showTopRightF2ShortcutButton && selectRoomPanel == null && roomInfoPanel == null)
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
            _windowRect = GUI.Window(94821, _windowRect, DrawLobbyWindowContents, "JUNK CYCLONE CRISIS - 1-6 PLAYER CO-OP [F2]", _windowStyle);
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
                string transportLabel = EmpireNetworkManager.Instance.IsSteamRelayActive
                    ? "STEAM RELAY ONLINE + LAN ROOM CODE:"
                    : "LAN ROOM CODE & ENDPOINT:";
                GUILayout.Label(transportLabel, _subHeaderStyle);
                string codeDisplay = EmpireNetworkManager.Instance.IsSteamRelayActive
                    ? $"{EmpireNetworkManager.Instance.ActiveJoinCode}   (STEAM RELAY)"
                    : $"{EmpireNetworkManager.Instance.ActiveJoinCode}   ({EmpireNetworkManager.Instance.ActiveHostIp}:{EmpireNetworkManager.Instance.ActivePort})";
                GUILayout.Box(codeDisplay, _codeBoxStyle, GUILayout.Height(38));

                GUILayout.BeginHorizontal();
                GUI.backgroundColor = new Color(0.25f, 0.65f, 0.95f);
                if (GUILayout.Button("Copy 6-Digit Code", _btnStyle, GUILayout.Height(28)))
                {
                    GUIUtility.systemCopyBuffer = EmpireNetworkManager.Instance.ActiveJoinCode;
                    _statusMessage = $"Copied Join Code '{EmpireNetworkManager.Instance.ActiveJoinCode}'!";
                }
                if (EmpireNetworkManager.Instance.HasActiveSteamLobby)
                {
                    GUI.backgroundColor = new Color(0.14f, 0.45f, 0.72f);
                    if (GUILayout.Button("Invite Steam Friend", _btnStyle, GUILayout.Height(28)))
                    {
                        EmpireNetworkManager.Instance.OpenSteamInviteOverlay();
                        _statusMessage = "Opened Steam Friend Invite overlay!";
                    }
                }
                else if (GUILayout.Button("Copy IP:Port", _btnStyle, GUILayout.Height(28)))
                {
                    GUIUtility.systemCopyBuffer = $"{EmpireNetworkManager.Instance.ActiveHostIp}:{EmpireNetworkManager.Instance.ActivePort}";
                    _statusMessage = $"Copied '{GUIUtility.systemCopyBuffer}'!";
                }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.Space(6);
            }
            else
            {
                GUILayout.BeginVertical(GUI.skin.box);
                string steamState = (EmpireNetworkManager.Instance != null && EmpireNetworkManager.Instance.IsSteamAvailable)
                    ? $"STEAM ONLINE: {EmpireNetworkManager.Instance.SteamPlayerName} (AppID 480)"
                    : "STEAM OFFLINE (Open Steam client for online Steam Relay)";
                GUILayout.Label(steamState, _subHeaderStyle);
                GUILayout.BeginHorizontal();
                _joinCodeInput = GUILayout.TextField(_joinCodeInput ?? "", GUILayout.Height(26));
                GUI.backgroundColor = new Color(0.16f, 0.72f, 0.45f);
                if (GUILayout.Button("Join Code/IP", _btnStyle, GUILayout.Height(26), GUILayout.Width(115)))
                {
                    EmpireNetworkManager.Instance?.StartClientSession(_joinCodeInput);
                    _statusMessage = $"Connecting to '{_joinCodeInput}'...";
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
                GUILayout.Label($"DISCOVERED ROOMS ({lanRooms.Count}):", _subHeaderStyle);
                for (int i = 0; i < lanRooms.Count; i++)
                {
                    var room = lanRooms[i];
                    GUI.backgroundColor = room.isSteamRelay ? new Color(0.14f, 0.52f, 0.85f) : new Color(0.16f, 0.78f, 0.55f);
                    string tag = room.isSteamRelay ? "[STEAM]" : "[LAN]";
                    string roomBtnText = $"JOIN {tag} [{room.roomName}] ({room.joinCode}) - {room.currentPlayers}/{room.maxPlayers}P";
                    if (GUILayout.Button(roomBtnText, _btnStyle, GUILayout.Height(30)))
                    {
                        string ep = LobbyRelayService.GetJoinEndpoint(room);
                        _joinCodeInput = room.joinCode;
                        EmpireNetworkManager.Instance?.StartClientSession(ep);
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

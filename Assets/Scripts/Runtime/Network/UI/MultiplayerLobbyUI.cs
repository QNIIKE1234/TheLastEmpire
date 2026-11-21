using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastEmpire.Network
{
    /// <summary>
    /// MultiplayerLobbyUI — หน้าต่างสร้างห้อง / กรอกรหัสเข้าห้อง 6 หลัก / เสกเพื่อนร่วมทีม Co-op (1–6 คน)
    /// - เปิด/ปิดได้ทุกเมื่อด้วยปุ่มลัด [F2] หรือกดปุ่ม "CO-OP [F2]" มุมขวาบนของหน้าจอ
    /// - รองรับ:
    ///   1. Host Co-op Room (สร้างห้อง 1–6 คน พร้อมแสดงรหัส Join Code 6 หลัก และปุ่ม Copy Code)
    ///   2. Join Co-op Room (กรอกรหัสห้อง 6 หลัก หรือ IP:Port เพื่อเข้าร่วมทีม)
    ///   3. Spawn Co-op Teammate Bot (+1 Ally สูงสุดครบ 6 คน เพื่อทดสอบระบบปาร์ตี้/ชุบชีวิต/ข้ามประตูได้ทันทีในจอเดียว)
    /// </summary>
    public class MultiplayerLobbyUI : MonoBehaviour
    {
        public static MultiplayerLobbyUI Instance { get; private set; }
        public static System.Action OnRequestLaunchSecondClientWindow;

        private bool _isWindowOpen = false;
        private Rect _windowRect = new Rect(0, 0, 480, 490);
        private string _joinCodeInput = "";
        private string _statusMessage = "Ready — Play Solo Offline or Host/Join 1–6 Player Co-op.";

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
        }

        private void Start()
        {
            _windowRect.x = (Screen.width - _windowRect.width) * 0.5f;
            _windowRect.y = (Screen.height - _windowRect.height) * 0.5f;
            _joinCodeInput = "127.0.0.1:7777";
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
            {
                ToggleLobbyWindow();
            }
        }

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

            // ปุ่มทางลัดมุมขวาบนสำหรับเปิดหน้าต่าง Co-op Lobby (หรือกด F2)
            int partyCount = Mathf.Max(1, PlayerRegistry.GetTotalPlayerCount());
            string badgeLabel = EmpireNetworkManager.IsOnline
                ? $"🌐 CO-OP ({partyCount}/6) [F2]"
                : $"🌐 CO-OP LOBBY [F2]";

            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = EmpireNetworkManager.IsOnline
                ? new Color(0.15f, 0.75f, 0.45f, 0.92f)
                : new Color(0.18f, 0.24f, 0.34f, 0.88f);

            if (GUI.Button(new Rect(Screen.width - 175f, 12f, 162f, 30f), badgeLabel, _badgeBtnStyle))
            {
                ToggleLobbyWindow();
            }
            GUI.backgroundColor = prevBg;

            if (!_isWindowOpen) return;

            GUI.backgroundColor = new Color(0.08f, 0.10f, 0.15f, 0.97f);
            _windowRect = GUI.Window(94821, _windowRect, DrawLobbyWindowContents, "THE LAST EMPIRE — 1-6 PLAYER CO-OP NETCODE [F2]", _windowStyle);
            GUI.backgroundColor = prevBg;
        }

        private void DrawLobbyWindowContents(int windowId)
        {
            GUILayout.Space(8);
            int totalPlayers = Mathf.Max(1, PlayerRegistry.GetTotalPlayerCount());
            string modeLabel = !EmpireNetworkManager.IsOnline
                ? "OFFLINE SINGLEPLAYER"
                : (EmpireNetworkManager.IsOnlineHost ? $"HOSTING ROOM ({totalPlayers}/6 PLAYERS)" : $"CONNECTED AS CLIENT ({totalPlayers}/6 PLAYERS)");

            GUILayout.Label($"Status: {modeLabel}", _headerStyle);
            GUILayout.Label($"Region (Auto QoS): {LobbyRelayService.ActiveRegion} (~{LobbyRelayService.EstimatedPingMs} ms)", _subHeaderStyle);
            GUILayout.Space(8);

            // 1. แสดงรหัสห้อง 6 หลักเมื่อเป็น Host
            if (EmpireNetworkManager.IsOnline && EmpireNetworkManager.Instance != null)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("ROOM JOIN CODE (6-DIGIT) & LAN ENDPOINT:", _subHeaderStyle);
                string codeDisplay = $"{EmpireNetworkManager.Instance.ActiveJoinCode}   ({EmpireNetworkManager.Instance.ActiveHostIp}:{EmpireNetworkManager.Instance.ActivePort})";
                GUILayout.Box(codeDisplay, _codeBoxStyle, GUILayout.Height(40));

                GUILayout.BeginHorizontal();
                GUI.backgroundColor = new Color(0.25f, 0.65f, 0.95f);
                if (GUILayout.Button("📋 Copy 6-Digit Code", _btnStyle, GUILayout.Height(30)))
                {
                    GUIUtility.systemCopyBuffer = EmpireNetworkManager.Instance.ActiveJoinCode;
                    _statusMessage = $"Copied Join Code '{EmpireNetworkManager.Instance.ActiveJoinCode}' to clipboard!";
                }
                if (GUILayout.Button("📋 Copy IP:Port", _btnStyle, GUILayout.Height(30)))
                {
                    GUIUtility.systemCopyBuffer = $"{EmpireNetworkManager.Instance.ActiveHostIp}:{EmpireNetworkManager.Instance.ActivePort}";
                    _statusMessage = $"Copied '{GUIUtility.systemCopyBuffer}' to clipboard!";
                }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.Space(8);
            }

            // 2. ปุ่มสร้างห้อง (Host) และกรอกรหัสเข้าห้อง (Join Client)
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("1. HOST OR JOIN MULTIPLAYER SESSION (UDP / NGO):", _subHeaderStyle);

            GUI.backgroundColor = new Color(0.18f, 0.80f, 0.42f);
            if (GUILayout.Button("★ HOST CO-OP GAME (1–6 PLAYERS)", _btnStyle, GUILayout.Height(36)))
            {
                if (EmpireNetworkManager.Instance != null && EmpireNetworkManager.Instance.StartHostSession())
                {
                    _statusMessage = $"Hosting room! Share Join Code: {EmpireNetworkManager.Instance.ActiveJoinCode}";
                }
            }

            if (OnRequestLaunchSecondClientWindow != null)
            {
                GUI.backgroundColor = new Color(0.15f, 0.72f, 0.95f);
                if (GUILayout.Button("🖥️ LAUNCH 2ND CLIENT WINDOW (Test 2 Clients on 1 PC)", _btnStyle, GUILayout.Height(34)))
                {
                    if (!EmpireNetworkManager.IsOnline && EmpireNetworkManager.Instance != null)
                    {
                        EmpireNetworkManager.Instance.StartHostSession();
                    }
                    OnRequestLaunchSecondClientWindow.Invoke();
                    _statusMessage = "Launching 2nd Client Window (auto-connecting to 127.0.0.1:7777)...";
                }
            }

            GUILayout.Space(6);
            GUILayout.Label("Enter 6-Digit Join Code or Host IP:Port:", _subHeaderStyle);
            GUILayout.BeginHorizontal();
            GUI.backgroundColor = Color.white;
            _joinCodeInput = GUILayout.TextField(_joinCodeInput, 32, _textFieldStyle, GUILayout.Height(34), GUILayout.Width(240));

            GUI.backgroundColor = new Color(0.22f, 0.62f, 0.96f);
            if (GUILayout.Button("▶ JOIN ROOM", _btnStyle, GUILayout.Height(34)))
            {
                if (EmpireNetworkManager.Instance != null && EmpireNetworkManager.Instance.StartClientSession(_joinCodeInput))
                {
                    _statusMessage = $"Connecting to {_joinCodeInput}...";
                }
                else
                {
                    _statusMessage = "Invalid Join Code or IP:Port format!";
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(8);

            // 3. ระบบเสกเพื่อนร่วมทีม Co-op จำลอง (Simulated Co-op Teammate Bot 1–6 คน)
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("2. INSTANT CO-OP PARTY TEST (SPAWN AI SQUADMATES 1–6P):", _subHeaderStyle);

            GUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.95f, 0.65f, 0.15f);
            if (GUILayout.Button($"➕ Spawn Co-op Teammate ({totalPlayers}/6)", _btnStyle, GUILayout.Height(34)))
            {
                if (EmpireNetworkManager.Instance != null)
                {
                    PlayerController bot = EmpireNetworkManager.Instance.SpawnSimulatedCoopTeammate();
                    if (bot != null)
                    {
                        _statusMessage = $"Spawned {bot.NetworkPlayerName} ({bot.ActiveCharacterData?.characterId}) into party!";
                    }
                    else
                    {
                        _statusMessage = "Party is at maximum capacity (6/6) or not in gameplay scene.";
                    }
                }
            }

            GUI.backgroundColor = new Color(0.75f, 0.35f, 0.25f);
            if (GUILayout.Button("✖ Clear Bots", _btnStyle, GUILayout.Height(34), GUILayout.Width(120)))
            {
                EmpireNetworkManager.Instance?.ClearSimulatedTeammates();
                _statusMessage = "Cleared simulated Co-op teammates.";
            }
            GUILayout.EndHorizontal();

            // ปุ่มทดสอบล้มรอเพื่อนชุบชีวิต (Downed & Revive Test)
            GUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.65f, 0.45f, 0.85f);
            if (GUILayout.Button("🩹 Test Downed State on First Teammate (Hold [E] to Revive)", _btnStyle, GUILayout.Height(30)))
            {
                var all = PlayerRegistry.AllPlayers;
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i] != null && all[i].IsRemotePlayer && !all[i].IsDowned)
                    {
                        Health h = all[i].PlayerHealth ?? all[i].GetComponent<Health>();
                        if (h != null)
                        {
                            h.TakeDamage(h.CurrentHealth + 999f, all[i].transform.position, false);
                            _statusMessage = $"{all[i].NetworkPlayerName} is DOWNED! Walk close and hold [E] for 3s to revive.";
                        }
                        break;
                    }
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(8);

            // 4. ปุ่มตัดการเชื่อมต่อ / ปิดหน้าต่าง
            GUILayout.BeginHorizontal();
            if (EmpireNetworkManager.IsOnline)
            {
                GUI.backgroundColor = new Color(0.88f, 0.25f, 0.28f);
                if (GUILayout.Button("⏹ Disconnect Session", _btnStyle, GUILayout.Height(34)))
                {
                    EmpireNetworkManager.Instance?.DisconnectSession();
                    _statusMessage = "Disconnected. Back to Singleplayer Offline mode.";
                }
            }

            GUI.backgroundColor = new Color(0.45f, 0.50f, 0.58f);
            if (GUILayout.Button("Close [F2]", _btnStyle, GUILayout.Height(34)))
            {
                _isWindowOpen = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUI.backgroundColor = Color.white;
            GUILayout.Label(_statusMessage, _subHeaderStyle);

            GUI.DragWindow(new Rect(0, 0, 10000, 28));
        }
    }
}

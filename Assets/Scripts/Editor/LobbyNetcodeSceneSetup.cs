#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TheLastEmpire.Network;

namespace TheLastEmpire.Editor
{
    /// <summary>
    /// LobbyNetcodeSceneSetup — แปะสคริปต์ MultiplayerLobbyUI ลงบน GameObject `RoomPanel`
    /// และสร้าง GameObject `EmpireNetworkManager` ในซีน `Lobby_NetCode.unity` ให้อัตโนมัติพร้อมผูก Reference ใน Inspector ครบทุกช่อง
    /// </summary>
    [InitializeOnLoad]
    public static class LobbyNetcodeSceneSetup
    {
        static LobbyNetcodeSceneSetup()
        {
            EditorApplication.delayCall += TrySetupActiveLobbyScene;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            TrySetupActiveLobbyScene();
        }

        [MenuItem("Tools/Multiplayer/4. Auto-Attach & Bind Scripts in Lobby_NetCode Scene", false, 20)]
        public static void TrySetupActiveLobbyScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded) return;

            GameObject canvasObj = GameObject.Find("MainMenuCanvas");
            if (canvasObj == null) return;

            Transform roomPanelTf = canvasObj.transform.Find("RoomPanel");
            if (roomPanelTf == null) return;

            bool modified = false;

            // 1. แปะ MultiplayerLobbyUI ลงบน RoomPanel โดยตรงเพื่อให้เห็นและปรับตั้งค่าใน Inspector ได้ชัดเจน
            MultiplayerLobbyUI lobbyUi = roomPanelTf.GetComponent<MultiplayerLobbyUI>();
            if (lobbyUi == null)
            {
                lobbyUi = Undo.AddComponent<MultiplayerLobbyUI>(roomPanelTf.gameObject);
                modified = true;
            }

            // ผูก Reference ทุกช่องลงใน Inspector (บันทึกเป็น fileID ของ Unity เมื่อเปลี่ยนชื่อ GameObject ก็ไม่หลุด)
            SerializedObject so = new SerializedObject(lobbyUi);
            if (so.FindProperty("_sceneRoomPanel")?.objectReferenceValue == null ||
                so.FindProperty("_roomThumbnailSlots")?.arraySize == 0)
            {
                lobbyUi.EditorAutoBindReferences();
                EditorUtility.SetDirty(lobbyUi);
                modified = true;
            }

            // 2. สร้าง GameObject `EmpireNetworkManager` ไว้ในซีน Lobby_NetCode เพื่อให้เห็นใน Hierarchy ชัดเจน
            EmpireNetworkManager netMgr = Object.FindFirstObjectByType<EmpireNetworkManager>();
            if (netMgr == null)
            {
                GameObject netGo = new GameObject("EmpireNetworkManager");
                netMgr = netGo.AddComponent<EmpireNetworkManager>();
                netGo.AddComponent<NetworkStageSyncManager>();
                netGo.AddComponent<NetworkEnemyBridge>();
                netGo.AddComponent<NetworkLootManager>();
                netGo.AddComponent<NetworkBuildingSyncManager>();
                netGo.AddComponent<PartyStatusHUD>();
                EditorUtility.SetDirty(netGo);
                modified = true;
            }

            // 3. ผูก MainMenuManager.characterSelectModalObj ชี้ไปที่ Lobby ในซีน
            MainMenuManager mmm = Object.FindFirstObjectByType<MainMenuManager>();
            Transform lobbyTf = canvasObj.transform.Find("Lobby");
            if (mmm != null && lobbyTf != null)
            {
                SerializedObject mmmSo = new SerializedObject(mmm);
                SerializedProperty charModalProp = mmmSo.FindProperty("characterSelectModalObj");
                if (charModalProp != null && charModalProp.objectReferenceValue == null)
                {
                    charModalProp.objectReferenceValue = lobbyTf.gameObject;
                    mmmSo.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(mmm);
                    modified = true;
                }
            }

            if (modified)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                Debug.Log("<color=#22C55E>[LobbyNetcodeSceneSetup]</color> Auto-attached MultiplayerLobbyUI onto 'RoomPanel', added 'EmpireNetworkManager' to scene, and bound all Inspector references in Lobby_NetCode.unity!");
            }
        }
    }
}
#endif

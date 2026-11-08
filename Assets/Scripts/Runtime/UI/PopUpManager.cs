using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TheLastEmpire
{
    public class PopUpManager : MonoBehaviour
    {
        public static PopUpManager Instance { get; private set; }

        private Stack<IPopUp> _popUpStack = new Stack<IPopUp>();

        public bool HasActivePopUps
        {
            get
            {
                while (_popUpStack.Count > 0 && _popUpStack.Peek() == null)
                {
                    _popUpStack.Pop();
                }
                return _popUpStack.Count > 0;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                GameObject obj = new GameObject("PopUpManager");
                Instance = obj.AddComponent<PopUpManager>();
                DontDestroyOnLoad(obj);
            }
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                // ถ้ากำลังอยู่ในโหมดเล็งสร้างสิ่งปลูกสร้าง ให้ BuildingPlacer ทำหน้าที่กดยกเลิกการสร้าง ไม่เปิดหน้าต่าง Setting หรือ Pop
                if (BuildingPlacer.Instance != null && BuildingPlacer.Instance.IsInBuildMode)
                {
                    return;
                }

                if (HasActivePopUps)
                {
                    Pop();
                }
                else
                {
                    // ถ้าไม่มี PopUp ใดๆ เปิดอยู่ และอยู่ใน Gameplay (ไม่ใช่ MainMenu) ให้กด ESC เพื่อเปิดหน้าต่าง Setting
                    string sceneName = SceneManager.GetActiveScene().name;
                    if (!sceneName.Equals("MainMenu", System.StringComparison.OrdinalIgnoreCase))
                    {
                        if (SettingManager.Instance != null)
                        {
                            SettingManager.Instance.ToggleSettingsModal();
                        }
                    }
                }
            }
        }

        public void Push(IPopUp popup)
        {
            if (popup != null && !_popUpStack.Contains(popup))
            {
                _popUpStack.Push(popup);
            }
        }

        public void Pop()
        {
            while (_popUpStack.Count > 0)
            {
                IPopUp popup = _popUpStack.Pop();
                if (popup != null)
                {
                    popup.ClosePopUp();
                    break;
                }
            }
        }

        public void Remove(IPopUp popup)
        {
            if (_popUpStack.Contains(popup))
            {
                // To remove from middle of stack, we have to rebuild it
                Stack<IPopUp> tempStack = new Stack<IPopUp>();
                while (_popUpStack.Count > 0)
                {
                    IPopUp top = _popUpStack.Pop();
                    if (top == popup)
                        break; // Found and removed
                    tempStack.Push(top);
                }
                
                // Put back the rest
                while (tempStack.Count > 0)
                {
                    _popUpStack.Push(tempStack.Pop());
                }
            }
        }
    }
}

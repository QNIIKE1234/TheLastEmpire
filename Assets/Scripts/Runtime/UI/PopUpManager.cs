using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastEmpire
{
    public class PopUpManager : MonoBehaviour
    {
        public static PopUpManager Instance { get; private set; }

        private Stack<IPopUp> _popUpStack = new Stack<IPopUp>();

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
                if (_popUpStack.Count > 0)
                {
                    Pop();
                }
            }
        }

        public void Push(IPopUp popup)
        {
            if (!_popUpStack.Contains(popup))
            {
                _popUpStack.Push(popup);
            }
        }

        public void Pop()
        {
            if (_popUpStack.Count > 0)
            {
                IPopUp popup = _popUpStack.Pop();
                popup.ClosePopUp();
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

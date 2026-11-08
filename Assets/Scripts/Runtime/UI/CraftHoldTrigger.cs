using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TheLastEmpire
{
    /// <summary>
    /// Helper component บนปุ่ม UI สำหรับดักจับการกดค้าง (Hold to Craft)
    /// ผ่าน PointerDown, PointerUp, PointerExit ของ Unity EventSystem
    /// </summary>
    public class CraftHoldTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private Action _onDown;
        private Action _onUp;
        private Action _onExit;

        public void Initialize(Action onDown, Action onUp, Action onExit)
        {
            _onDown = onDown;
            _onUp = onUp;
            _onExit = onExit;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _onDown?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _onUp?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _onExit?.Invoke();
        }
    }
}

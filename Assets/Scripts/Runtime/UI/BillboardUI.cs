using UnityEngine;

namespace TheLastEmpire
{
    public class BillboardUI : MonoBehaviour
    {
        private Transform _camTransform;

        private void Start()
        {
            if (Camera.main != null)
            {
                _camTransform = Camera.main.transform;

                // Automatically link the Canvas's Event Camera (worldCamera) at runtime
                Canvas canvas = GetComponent<Canvas>();
                if (canvas != null && canvas.worldCamera == null)
                {
                    canvas.worldCamera = Camera.main;
                }
            }
        }

        private void LateUpdate()
        {
            if (_camTransform != null)
            {
                // Align rotation to match the camera's rotation so it stays flat and faces the camera
                transform.LookAt(transform.position + _camTransform.rotation * Vector3.forward,
                                 _camTransform.rotation * Vector3.up);
            }
        }
    }
}

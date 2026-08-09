using UnityEngine;

namespace TheLastEmpire
{
    public class ThrownEntity : MonoBehaviour
    {
        private Vector3 _startPos;
        private Vector3 _targetPos;
        private float _flightDuration;
        private float _currentFlightTime;
        private float _arcHeight;
        private System.Action _onLanding;
        
        private Rigidbody _rb;
        private bool _wasGravity;
        private bool _wasKinematic;

        public void Setup(Vector3 target, float duration, float height, System.Action onLand)
        {
            _startPos = transform.position;
            _targetPos = target;
            _flightDuration = duration;
            _arcHeight = height;
            _currentFlightTime = 0f;
            _onLanding = onLand;

            _rb = GetComponent<Rigidbody>();
            if (_rb != null)
            {
                _wasGravity = _rb.useGravity;
                _wasKinematic = _rb.isKinematic;
                _rb.useGravity = false;
                _rb.isKinematic = true; // Prevent physics interference during manual lerp
                _rb.linearVelocity = Vector3.zero;
            }
        }

        private void Update()
        {
            _currentFlightTime += Time.deltaTime;
            float t = _currentFlightTime / _flightDuration;

            if (t >= 1f)
            {
                transform.position = _targetPos;
                Land();
                return;
            }

            Vector3 currentPos = Vector3.Lerp(_startPos, _targetPos, t);
            currentPos.y += Mathf.Sin(t * Mathf.PI) * _arcHeight;
            transform.position = currentPos;
            
            // Adding a slight spin effect
            transform.Rotate(Vector3.up * 720f * Time.deltaTime, Space.World);
        }

        private void Land()
        {
            if (_rb != null)
            {
                _rb.useGravity = _wasGravity;
                _rb.isKinematic = _wasKinematic;
                _rb.linearVelocity = Vector3.zero;
            }
            
            // Snap to upright rotation
            transform.rotation = Quaternion.identity;

            _onLanding?.Invoke();
            Destroy(this); // Remove this script once landed
        }
    }
}

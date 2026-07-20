using UnityEngine;

namespace TheLastEmpire
{
    public class EnemyHealthText : MonoBehaviour
    {
        private Health _health;
        private TextMesh _textMesh;

        private void Start()
        {
            _health = GetComponent<Health>();

            // Create a child object to render the text
            GameObject textObj = new GameObject("HUD_EnemyHealthText");
            textObj.transform.SetParent(transform, false);
            // Position slightly above the enemy's head
            textObj.transform.localPosition = new Vector3(0f, 1.1f, 0f);

            _textMesh = textObj.AddComponent<TextMesh>();
            _textMesh.characterSize = 0.07f; // Compact readable sizing
            _textMesh.fontSize = 28;
            _textMesh.color = new Color(1f, 0.25f, 0.25f, 0.9f); // High-contrast warning red
            _textMesh.alignment = TextAlignment.Center;
            _textMesh.anchor = TextAnchor.MiddleCenter;

            // Use the standard Arial font that is bundled with Unity
            _textMesh.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            MeshRenderer mr = textObj.GetComponent<MeshRenderer>();
            if (mr != null && _textMesh.font != null)
            {
                mr.material = _textMesh.font.material;
            }

            if (_health != null)
            {
                _health.onHealthChanged.AddListener(UpdateText);
                UpdateText(_health.CurrentHealth);
            }
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.onHealthChanged.RemoveListener(UpdateText);
            }
        }

        private void LateUpdate()
        {
            // Anchor rotation upright so it does not spin upside-down as the enemy rotates to aim
            if (_textMesh != null)
            {
                _textMesh.transform.rotation = Quaternion.identity;
            }
        }

        private void UpdateText(float currentHealth)
        {
            if (_textMesh != null && _health != null)
            {
                if (_health.IsDead)
                {
                    _textMesh.text = "";
                }
                else
                {
                    _textMesh.text = $"{Mathf.RoundToInt(currentHealth)} / {Mathf.RoundToInt(_health.MaxHealth)}";
                }
            }
        }
    }
}

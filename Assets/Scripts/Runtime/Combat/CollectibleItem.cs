using UnityEngine;
using TMPro;

namespace TheLastEmpire
{
    public class CollectibleItem : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private string itemName = "Scrap Metal";
        [SerializeField] private int quantity = 1;
        [SerializeField] private int moneyAmount = 10;
        [SerializeField] private bool isMoney = false;
        [SerializeField] private float interactRange = 1.5f;

        public int Quantity => quantity;

        private Transform _playerTransform;
        private TextMeshPro _promptText;

        public string ItemName => itemName;
        public int MoneyAmount => moneyAmount;
        public bool IsMoney => isMoney;

        private void Start()
        {
            // Pop/hop force on spawn for aesthetic feel
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.gravityScale = 1f; // temporary gravity to fall back down
                Vector2 hopForce = new Vector2(Random.Range(-1f, 1f), Random.Range(3f, 5f));
                rb.linearVelocity = hopForce; // using Unity 6 linearVelocity
                StartCoroutine(DisableGravityDelayed(rb, 0.6f));
            }

            // Programmatically build TextMeshPro prompt so user doesn't have to manually configure UI
            GameObject textObj = new GameObject("InteractPrompt");
            textObj.transform.SetParent(transform);
            textObj.transform.localPosition = new Vector3(0f, 0.6f, 0f);

            _promptText = textObj.AddComponent<TextMeshPro>();
            _promptText.fontSize = 2.5f;
            _promptText.alignment = TextAlignmentOptions.Center;
            _promptText.color = Color.yellow;
            _promptText.gameObject.SetActive(false);

            UpdatePromptText();
            FindPlayer();
        }

        private System.Collections.IEnumerator DisableGravityDelayed(Rigidbody2D rb, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (rb != null)
            {
                rb.gravityScale = 0f;
                rb.linearVelocity = Vector2.zero;
            }
        }

        private void Update()
        {
            if (_playerTransform == null)
            {
                FindPlayer();
                return;
            }

            float dist = Vector2.Distance(transform.position, _playerTransform.position);
            bool inRange = dist <= interactRange;

            if (_promptText != null)
            {
                _promptText.gameObject.SetActive(inRange);
            }
        }

        private void FindPlayer()
        {
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                _playerTransform = player.transform;
            }
        }

        public bool IsPlayerInRange()
        {
            if (_playerTransform == null) return false;
            return Vector2.Distance(transform.position, _playerTransform.position) <= interactRange;
        }

        public void Collect()
        {
            // Visual pop effect (could spawn particle)
            Destroy(gameObject);
        }

        public void SetItemDetails(string name, int qty)
        {
            itemName = name;
            quantity = qty;

            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            
            // Check Database for custom configs (icons, colors, etc)
            ItemData data = (ItemDatabase.Instance != null) ? ItemDatabase.Instance.GetItemByName(name) : null;
            if (data != null)
            {
                if (sr != null)
                {
                    if (data.icon != null)
                    {
                        sr.sprite = data.icon;
                        // Reset tint to white so custom textures render properly
                        sr.color = Color.white; 
                    }
                    else
                    {
                        sr.color = data.themeColor;
                    }
                }
            }
            else
            {
                // Fallback to default hardcoded colors if no database entry exists
                if (sr != null)
                {
                    if (name == "Ammo")
                    {
                        sr.color = new Color(0f, 0.9f, 1f); // Vibrant light blue for Ammo
                    }
                    else if (name == "Potion")
                    {
                        sr.color = new Color(0.1f, 1f, 0.2f); // Healing green for Potion
                    }
                    else if (name == "Bread")
                    {
                        sr.color = new Color(0.95f, 0.75f, 0.5f); // Warm tan for Bread
                    }
                    else if (name == "ETC")
                    {
                        sr.color = new Color(1f, 0.6f, 0f); // Crafting orange for ETC
                    }
                }
            }

            UpdatePromptText();
        }

        public void SetMoneyDetails(int amount)
        {
            isMoney = true;
            moneyAmount = amount;
            UpdatePromptText();
        }

        private void UpdatePromptText()
        {
            if (_promptText != null)
            {
                if (isMoney)
                {
                    _promptText.text = $"Press [E] (${moneyAmount})";
                }
                else
                {
                    _promptText.text = $"Press [E] ({itemName} x{quantity})";
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactRange);
        }
    }
}

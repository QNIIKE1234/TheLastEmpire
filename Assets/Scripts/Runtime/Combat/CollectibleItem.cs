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
        [SerializeField] private TMP_Text promptText;
        [SerializeField] private MeshRenderer itemMeshRenderer;

        public string ItemName => itemName;
        public int MoneyAmount => moneyAmount;
        public bool IsMoney => isMoney;

        private void Start()
        {
            // Pop/hop force on spawn for aesthetic feel
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = true; // temporary gravity to fall back down
                Vector3 hopForce = new Vector3(Random.Range(-1f, 1f), Random.Range(3f, 5f), Random.Range(-1f, 1f));
                rb.linearVelocity = hopForce; // using Unity 6 linearVelocity
                StartCoroutine(DisableGravityDelayed(rb, 0.6f));
            }

            if (promptText != null)
            {
                promptText.gameObject.SetActive(false);
            }

            UpdatePromptText();
            FindPlayer();
        }

        private System.Collections.IEnumerator DisableGravityDelayed(Rigidbody rb, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
            }
        }

        private void Update()
        {
            if (_playerTransform == null)
            {
                FindPlayer();
                return;
            }

            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            bool inRange = dist <= interactRange;

            if (promptText != null)
            {
                promptText.gameObject.SetActive(inRange);
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
            return Vector3.Distance(transform.position, _playerTransform.position) <= interactRange;
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
                Debug.Log($"[CollectibleItem] SetItemDetails: data found for '{name}'. Material is {(data.dropMaterial != null ? data.dropMaterial.name : "null")}");
                // Apply 3D Material if available
                if (data.dropMaterial != null)
                {
                    MeshRenderer mr = itemMeshRenderer != null ? itemMeshRenderer : (GetComponent<MeshRenderer>() ?? GetComponentInChildren<MeshRenderer>(true));
                    if (mr != null)
                    {
                        mr.sharedMaterial = data.dropMaterial;
                        Debug.Log($"[CollectibleItem] Applied material '{data.dropMaterial.name}' to {mr.gameObject.name}");
                    }
                    else
                    {
                        System.Text.StringBuilder sb = new System.Text.StringBuilder();
                        sb.AppendLine($"[CollectibleItem] MeshRenderer not found on self or children for '{name}'!");
                        sb.AppendLine("Components on self:");
                        foreach (Component c in GetComponents<Component>())
                        {
                            if (c != null) sb.AppendLine($"- {c.GetType().Name}");
                        }
                        sb.AppendLine("Children hierarchy:");
                        foreach (Transform child in transform)
                        {
                            sb.AppendLine($"- Child: {child.name} (Active: {child.gameObject.activeSelf})");
                            foreach (Component comp in child.GetComponents<Component>())
                            {
                                if (comp != null) sb.AppendLine($"  * Comp: {comp.GetType().Name}");
                            }
                        }
                        Debug.LogWarning(sb.ToString());
                    }
                }
                if (sr != null)
                {
                    if (data.icon != null)
                    {
                        sr.sprite = data.icon;
                        // Reset tint to white so custom textures render properly
                        sr.color = Color.white; 

                        // Normalize scale so large sprites don't take up the whole screen
                        float spriteWidth = data.icon.rect.width / data.icon.pixelsPerUnit;
                        float spriteHeight = data.icon.rect.height / data.icon.pixelsPerUnit;
                        float maxDimension = Mathf.Max(spriteWidth, spriteHeight);
                        if (maxDimension > 0f)
                        {
                            // Target world size: 0.45 meters
                            float targetScale = 0.45f / maxDimension;
                            transform.localScale = new Vector3(targetScale, targetScale, 1f);
                        }
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

            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            ItemData data = (ItemDatabase.Instance != null) ? ItemDatabase.Instance.GetItemByName("Money") : null;
            if (data != null)
            {
                Debug.Log($"[CollectibleItem] SetMoneyDetails: data found for 'Money'. Material is {(data.dropMaterial != null ? data.dropMaterial.name : "null")}");
                // Apply 3D Material if available
                if (data.dropMaterial != null)
                {
                    MeshRenderer mr = itemMeshRenderer != null ? itemMeshRenderer : (GetComponent<MeshRenderer>() ?? GetComponentInChildren<MeshRenderer>(true));
                    if (mr != null)
                    {
                        mr.sharedMaterial = data.dropMaterial;
                        Debug.Log($"[CollectibleItem] Applied material '{data.dropMaterial.name}' to {mr.gameObject.name}");
                    }
                    else
                    {
                        System.Text.StringBuilder sb = new System.Text.StringBuilder();
                        sb.AppendLine("[CollectibleItem] MeshRenderer not found on self or children for 'Money'!");
                        sb.AppendLine("Components on self:");
                        foreach (Component c in GetComponents<Component>())
                        {
                            if (c != null) sb.AppendLine($"- {c.GetType().Name}");
                        }
                        sb.AppendLine("Children hierarchy:");
                        foreach (Transform child in transform)
                        {
                            sb.AppendLine($"- Child: {child.name} (Active: {child.gameObject.activeSelf})");
                            foreach (Component comp in child.GetComponents<Component>())
                            {
                                if (comp != null) sb.AppendLine($"  * Comp: {comp.GetType().Name}");
                            }
                        }
                        Debug.LogWarning(sb.ToString());
                    }
                }

                if (data.icon != null && sr != null)
                {
                    sr.sprite = data.icon;
                    sr.color = Color.white;

                    // Normalize scale so large sprites don't take up the whole screen
                    float spriteWidth = data.icon.rect.width / data.icon.pixelsPerUnit;
                    float spriteHeight = data.icon.rect.height / data.icon.pixelsPerUnit;
                    float maxDimension = Mathf.Max(spriteWidth, spriteHeight);
                    if (maxDimension > 0f)
                    {
                        float targetScale = 0.4f / maxDimension; // Slightly smaller for coins
                        transform.localScale = new Vector3(targetScale, targetScale, 1f);
                    }
                }
            }
            else
            {
                if (sr != null)
                {
                    sr.color = Color.yellow;
                }
            }

            UpdatePromptText();
        }

        private void UpdatePromptText()
        {
            if (promptText != null)
            {
                if (isMoney)
                {
                    promptText.text = $"Press [E] (${moneyAmount})";
                }
                else
                {
                    promptText.text = $"Press [E] ({itemName} x{quantity})";
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

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

        public const float GROUND_Y = 0.25f;
        private bool _skipHop = false;
        private bool _isPopping = false;

        private void Start()
        {
            // Ensure all colliders are triggers immediately so all units walk through freely
            Collider[] colliders = GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                if (col != null) col.isTrigger = true;
            }

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.useGravity = false;
                rb.isKinematic = true;
            }

            // Immediately ignore physical collisions with all enemy colliders so zombies walk through
            IgnoreEnemyCollisions();

            if (promptText != null)
            {
                promptText.gameObject.SetActive(false);
            }

            UpdatePromptText();
            FindPlayer();

            if (_skipHop)
            {
                SnapToGround();
            }
            else
            {
                StartCoroutine(PopAndLandRoutine());
            }
        }

        private System.Collections.IEnumerator PopAndLandRoutine()
        {
            _isPopping = true;
            Vector3 startPos = transform.position;
            Vector2 randomScatter = Random.insideUnitCircle * 0.6f;
            Vector3 targetPos = new Vector3(startPos.x + randomScatter.x, GROUND_Y, startPos.z + randomScatter.y);

            float duration = 0.45f;
            float elapsed = 0f;
            float arcHeight = 0.65f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float currentX = Mathf.Lerp(startPos.x, targetPos.x, t);
                float currentZ = Mathf.Lerp(startPos.z, targetPos.z, t);
                float currentY = Mathf.Lerp(startPos.y, targetPos.y, t) + Mathf.Sin(t * Mathf.PI) * arcHeight;

                transform.position = new Vector3(currentX, Mathf.Max(GROUND_Y, currentY), currentZ);
                yield return null;
            }

            transform.position = targetPos;
            _isPopping = false;
        }

        public void SnapToGround()
        {
            _skipHop = true;
            _isPopping = false;
            StopAllCoroutines();

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.useGravity = false;
                rb.isKinematic = true;
            }
            Collider[] colliders = GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                if (col != null) col.isTrigger = true;
            }
            Vector3 p = transform.position;
            transform.position = new Vector3(p.x, GROUND_Y, p.z);
        }

        private void OnCollisionEnter(Collision collision)
        {
            // If an enemy bumps into the item while in mid-air, ignore collision
            if (collision.gameObject.CompareTag("Enemy") || collision.gameObject.GetComponent<BaseEnemyAI>() != null)
            {
                Collider[] myColliders = GetComponentsInChildren<Collider>();
                foreach (var c in myColliders)
                {
                    if (c != null && collision.collider != null)
                    {
                        Physics.IgnoreCollision(c, collision.collider, true);
                    }
                }
            }
        }

        public void IgnoreEnemyCollisions()
        {
            Collider[] myColliders = GetComponentsInChildren<Collider>();
            BaseEnemyAI[] enemies = Object.FindObjectsByType<BaseEnemyAI>(FindObjectsSortMode.None);
            foreach (var enemy in enemies)
            {
                if (enemy == null) continue;
                Collider[] enemyCols = enemy.GetComponentsInChildren<Collider>();
                foreach (var myCol in myColliders)
                {
                    if (myCol == null) continue;
                    foreach (var enemyCol in enemyCols)
                    {
                        if (enemyCol != null)
                        {
                            Physics.IgnoreCollision(myCol, enemyCol, true);
                        }
                    }
                }
            }
        }

        private void Update()
        {
            // Safety ground clamp if not in the middle of popping arc
            if (!_isPopping && Mathf.Abs(transform.position.y - GROUND_Y) > 0.05f)
            {
                Vector3 pos = transform.position;
                pos.y = GROUND_Y;
                transform.position = pos;
            }

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

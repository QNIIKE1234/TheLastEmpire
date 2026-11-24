using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// Architectural staircase inside multi-floor building interiors (Floors 1..4).
    /// Local +Z points INTO the room (inward normal), so steps start low inside the room (+Z)
    /// and climb UPWARD into the wall doorway (-Z) for Going Up, or descend cleanly into a
    /// stairwell landing for Going Down!
    /// Pressing [E] transitions smoothly between floors.
    /// </summary>
    public class BuildingStairsPortal : MonoBehaviour, IBuildingInteractable
    {
        public bool IsGoingUp { get; private set; } = true;
        public int TargetFloorIndex { get; private set; } = 2;

        private Light _stairLight;
        private Material _stepAccentMat;
        private Color _glowColor = new Color(0.35f, 0.82f, 1f, 1f);

        public Transform InteractionTransform => transform;
        public Vector3 PromptWorldOffset => new Vector3(0f, 1.85f, 0.35f);
        public float InteractionDistance => 2.6f;

        public bool CanInteract =>
            isActiveAndEnabled &&
            gameObject.activeInHierarchy &&
            BuildingInteriorManager.IsInsideInterior &&
            !BuildingInteriorManager.IsTransitioning;

        private void OnEnable()
        {
            BuildingInteractionRegistry.Register(this);
        }

        private void OnDisable()
        {
            BuildingInteractionRegistry.Unregister(this);
        }

        private void OnDestroy()
        {
            BuildingInteractionRegistry.Unregister(this);
            if (_stepAccentMat != null)
            {
                Destroy(_stepAccentMat);
                _stepAccentMat = null;
            }
        }

        public void Configure(bool goingUp, int targetFloorIndex)
        {
            IsGoingUp = goingUp;
            TargetFloorIndex = targetFloorIndex;
            _glowColor = goingUp
                ? new Color(0.32f, 0.85f, 1.0f, 1f)   // Cool cyan-blue for Stairs Up
                : new Color(1.0f, 0.72f, 0.28f, 1f);  // Warm amber for Stairs Down

            BuildStaircaseVisual();
        }

        private void BuildStaircaseVisual()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            Material woodTreadMat = MakeMat(shader, new Color(0.48f, 0.34f, 0.22f), 0.24f);
            Material woodRiserMat = MakeMat(shader, new Color(0.28f, 0.20f, 0.14f), 0.18f);
            Material railMetalMat = MakeMat(shader, new Color(0.22f, 0.24f, 0.27f), 0.55f);
            Material topRailWood = MakeMat(shader, new Color(0.38f, 0.25f, 0.15f), 0.30f);

            _stepAccentMat = MakeMat(shader, _glowColor * 0.85f, 0.30f);
            if (_stepAccentMat != null && _stepAccentMat.HasProperty("_EmissionColor"))
            {
                _stepAccentMat.EnableKeyword("_EMISSION");
                _stepAccentMat.SetColor("_EmissionColor", _glowColor * 1.1f);
            }

            // Local +Z faces INTO the room; Local -Z faces INTO the wall doorway opening!
            // Therefore:
            // - Going UP: step 0 starts low at +Z (room side) and climbs UP toward -Z (into doorway)!
            // - Going DOWN: clean landing platform at +Z with recessed dark stairwell stepping down into -Z!
            int stepCount = 7;
            float stairWidth = 1.72f;
            float stepRun = 0.26f;
            float totalRun = stepCount * stepRun;
            float zStartRoomSide = totalRun * 0.5f;

            // Welcome threshold pad at the room-side foot of the stairs
            Part("FootLandingMat", new Vector3(0f, 0.02f, zStartRoomSide + 0.34f), new Vector3(stairWidth * 0.92f, 0.03f, 0.56f), _stepAccentMat);

            for (int i = 0; i < stepCount; i++)
            {
                float z = zStartRoomSide - (i + 0.5f) * stepRun;
                if (IsGoingUp)
                {
                    float topY = 0.16f + i * 0.22f;
                    // Riser block beneath tread
                    Part($"Riser_{i}", new Vector3(0f, topY * 0.5f, z), new Vector3(stairWidth, topY, stepRun * 0.96f), woodRiserMat);
                    // Overhanging wooden tread plank
                    Part($"Tread_{i}", new Vector3(0f, topY + 0.025f, z + 0.02f), new Vector3(stairWidth + 0.06f, 0.05f, stepRun + 0.04f), woodTreadMat);
                    // Crisp subtle edge accent strip on the front nosing (room-facing edge)
                    if (i % 2 == 0 || i == stepCount - 1)
                    {
                        Part($"NoseGlow_{i}", new Vector3(0f, topY + 0.052f, z + stepRun * 0.44f), new Vector3(stairWidth * 0.88f, 0.018f, 0.045f), _stepAccentMat);
                    }
                }
                else
                {
                    // Descending steps toward doorway (-Z)
                    float topY = Mathf.Max(0.06f, 0.38f - i * 0.052f);
                    Part($"DownStep_{i}", new Vector3(0f, topY * 0.5f, z), new Vector3(stairWidth, topY, stepRun * 0.94f), woodTreadMat);
                    if (i % 2 == 0)
                    {
                        Part($"DownGlow_{i}", new Vector3(0f, topY + 0.015f, z - stepRun * 0.36f), new Vector3(stairWidth * 0.88f, 0.018f, 0.045f), _stepAccentMat);
                    }
                }
            }

            // Sleek open-baluster handrails along left and right sides (NO chunky solid side walls!)
            float railX = stairWidth * 0.5f + 0.06f;
            int postCount = 4;
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * railX;
                for (int p = 0; p < postCount; p++)
                {
                    float frac = (float)p / (postCount - 1);
                    float pz = Mathf.Lerp(zStartRoomSide - 0.08f, -zStartRoomSide + 0.08f, frac);
                    float baseY = IsGoingUp ? Mathf.Lerp(0.14f, 1.48f, frac) : Mathf.Lerp(0.36f, 0.08f, frac);
                    float postH = 0.78f;
                    Part($"Post_{side}_{p}", new Vector3(x, baseY + postH * 0.5f, pz), new Vector3(0.07f, postH, 0.07f), railMetalMat);
                }

                // Sloped continuous handrail bar connecting the posts cleanly
                float startY = IsGoingUp ? (0.14f + 0.78f) : (0.36f + 0.78f);
                float endY = IsGoingUp ? (1.48f + 0.78f) : (0.08f + 0.78f);
                Vector3 a = new Vector3(x, startY, zStartRoomSide - 0.08f);
                Vector3 b = new Vector3(x, endY, -zStartRoomSide + 0.08f);
                Vector3 mid = (a + b) * 0.5f;
                float len = Vector3.Distance(a, b) + 0.12f;

                GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.name = $"Handrail_{side}";
                rail.transform.SetParent(transform, false);
                rail.transform.localPosition = mid;
                rail.transform.localRotation = Quaternion.LookRotation((b - a).normalized, Vector3.up);
                rail.transform.localScale = new Vector3(0.09f, 0.07f, len);
                Collider rc = rail.GetComponent<Collider>();
                if (rc != null) Destroy(rc);
                Renderer rr = rail.GetComponent<Renderer>();
                if (rr != null && topRailWood != null) rr.sharedMaterial = topRailWood;
            }

            // Glowing overhead indicator bar at the doorway end (-Z)
            Part("DoorwayIndicatorStrip", new Vector3(0f, 2.08f, -zStartRoomSide - 0.08f), new Vector3(stairWidth * 0.72f, 0.09f, 0.09f), _stepAccentMat);

            // Soft directional beacon light
            GameObject lightObj = new GameObject("StairsBeaconLight");
            lightObj.transform.SetParent(transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 1.85f, 0.25f);
            _stairLight = lightObj.AddComponent<Light>();
            _stairLight.type = LightType.Point;
            _stairLight.color = _glowColor;
            _stairLight.range = 4.2f;
            _stairLight.intensity = 1.6f;
            _stairLight.shadows = LightShadows.None;
        }

        private static Material MakeMat(Shader shader, Color col, float smoothness)
        {
            Material m = shader != null ? new Material(shader) : new Material(Shader.Find("Standard"));
            m.color = col;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        private void Part(string partName, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = partName;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
        }

        private void Update()
        {
            if (_stairLight == null) return;
            float pulse = 0.86f + 0.14f * Mathf.Sin(Time.time * 2.6f);
            _stairLight.intensity = 1.6f * pulse;
        }

        public string GetInteractionPrompt()
        {
            return IsGoingUp ? $"[E] Go Up to Floor {TargetFloorIndex}" : $"[E] Go Down to Floor {TargetFloorIndex}";
        }

        public void OnInteract()
        {
            if (!CanInteract) return;
            BuildingInteriorManager.Instance?.TransitionToFloor(TargetFloorIndex, arrivedFromBelow: IsGoingUp);
        }

        public void OnSecondaryInteract() { }
    }
}

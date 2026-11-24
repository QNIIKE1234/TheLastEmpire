using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// Controls the 3 back-wall windows inside building interiors so players can see the
    /// live exterior time of day (Dawn / Day / Dusk / Night) and watch sunlight or moonlight
    /// beams shine into the room onto the floor in real time!
    /// </summary>
    public class InteriorWindowDayNightController : MonoBehaviour
    {
        private readonly List<Material> _glassMaterials = new List<Material>();
        private readonly List<Material> _beamMaterials = new List<Material>();
        private readonly List<Light> _windowLights = new List<Light>();

        public void BuildThreeBackWallWindows(Transform parent, float roomW, float roomD, float wallH, Shader litShader, Material trimMat)
        {
            float halfD = roomD * 0.5f;
            float winY = wallH * 0.60f;
            float winW = Mathf.Clamp(roomW * 0.21f, 2.0f, 3.3f);
            float winH = Mathf.Clamp(wallH * 0.36f, 0.95f, 1.30f);
            float spacing = roomW * 0.29f;
            float[] winXs = { -spacing, 0f, spacing };

            Shader transparentShader = Shader.Find("Universal Render Pipeline/Unlit")
                                    ?? Shader.Find("Sprites/Default")
                                    ?? Shader.Find("Unlit/Transparent")
                                    ?? litShader;

            for (int i = 0; i < 3; i++)
            {
                float wx = winXs[i];

                // 1. Outer window frame + sill + middle mullion bar
                CreateBox(parent, $"BackWinFrame_{i}", new Vector3(wx, winY, halfD - 0.01f), new Vector3(winW + 0.22f, winH + 0.20f, 0.14f), trimMat);
                CreateBox(parent, $"BackWinSill_{i}", new Vector3(wx, winY - winH * 0.5f - 0.08f, halfD - 0.10f), new Vector3(winW + 0.32f, 0.10f, 0.24f), trimMat);
                CreateBox(parent, $"BackWinMullionV_{i}", new Vector3(wx, winY, halfD - 0.07f), new Vector3(0.08f, winH, 0.08f), trimMat);
                CreateBox(parent, $"BackWinMullionH_{i}", new Vector3(wx, winY, halfD - 0.07f), new Vector3(winW, 0.07f, 0.08f), trimMat);

                // 2. Live sky-tinted emissive glass pane (shows Dawn / Day / Dusk / Night outside!)
                Material glassMat = new Material(litShader ?? Shader.Find("Standard"));
                glassMat.EnableKeyword("_EMISSION");
                _glassMaterials.Add(glassMat);
                CreateBox(parent, $"BackWinGlass_{i}", new Vector3(wx, winY, halfD - 0.05f), new Vector3(winW, winH, 0.05f), glassMat);

                // 3. Soft light pool projected onto the interior floor directly beneath the window
                Material beamMat = CreateTransparentBeamMaterial(transparentShader);
                _beamMaterials.Add(beamMat);

                GameObject poolObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                poolObj.name = $"WindowFloorLightPool_{i}";
                poolObj.transform.SetParent(parent, false);
                poolObj.transform.localPosition = new Vector3(wx, 0.035f, halfD - 1.75f);
                poolObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                poolObj.transform.localScale = new Vector3(winW * 1.15f, 3.1f, 1f);
                Collider pc = poolObj.GetComponent<Collider>();
                if (pc != null) Destroy(pc);
                Renderer pr = poolObj.GetComponent<Renderer>();
                if (pr != null)
                {
                    pr.sharedMaterial = beamMat;
                    pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    pr.receiveShadows = false;
                }

                // 4. Slanted volumetric-style light shaft plane angled from window down to floor
                GameObject rayObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                rayObj.name = $"WindowLightShaft_{i}";
                rayObj.transform.SetParent(parent, false);
                rayObj.transform.localPosition = new Vector3(wx, winY * 0.52f, halfD - 1.15f);
                rayObj.transform.localRotation = Quaternion.Euler(36f, 180f, 0f);
                rayObj.transform.localScale = new Vector3(winW * 0.96f, winY * 1.35f, 1f);
                Collider rc = rayObj.GetComponent<Collider>();
                if (rc != null) Destroy(rc);
                Renderer rr = rayObj.GetComponent<Renderer>();
                if (rr != null)
                {
                    rr.sharedMaterial = beamMat;
                    rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    rr.receiveShadows = false;
                }

                // 5. Real SpotLight pouring sunlight / moonlight through the window onto interior furniture
                GameObject spotObj = new GameObject($"WindowSpotLight_{i}");
                spotObj.transform.SetParent(parent, false);
                spotObj.transform.localPosition = new Vector3(wx, winY + 0.1f, halfD - 0.25f);
                spotObj.transform.localRotation = Quaternion.Euler(46f, 180f, 0f);
                Light spot = spotObj.AddComponent<Light>();
                spot.type = LightType.Spot;
                spot.spotAngle = 72f;
                spot.range = Mathf.Max(8.5f, roomD * 0.78f);
                spot.intensity = 2.6f;
                spot.shadows = LightShadows.None;
                _windowLights.Add(spot);
            }

            ApplyCurrentExteriorLighting(immediate: true);
        }

        private void Update()
        {
            ApplyCurrentExteriorLighting(immediate: false);
        }

        private void ApplyCurrentExteriorLighting(bool immediate)
        {
            Color skyColor = new Color(0.68f, 0.88f, 1.0f, 1f);
            Color emissionColor = new Color(0.55f, 0.80f, 1.0f, 1f) * 1.45f;
            Color shaftColor = new Color(1.0f, 0.94f, 0.76f, 0.16f);
            Color lightColor = new Color(1.0f, 0.95f, 0.82f, 1f);
            float lightIntensity = 2.8f;

            DayNightManager dn = DayNightManager.Instance;
            if (dn != null)
            {
                switch (dn.CurrentPhase)
                {
                    case DayNightManager.TimePhase.Night:
                        skyColor = new Color(0.08f, 0.14f, 0.30f, 1f);
                        emissionColor = new Color(0.14f, 0.25f, 0.52f, 1f) * 1.15f;
                        shaftColor = new Color(0.45f, 0.62f, 0.95f, 0.11f);
                        lightColor = new Color(0.42f, 0.58f, 0.95f, 1f);
                        lightIntensity = 1.65f;
                        break;

                    case DayNightManager.TimePhase.Dusk:
                        skyColor = new Color(0.96f, 0.52f, 0.24f, 1f);
                        emissionColor = new Color(1.0f, 0.48f, 0.18f, 1f) * 1.55f;
                        shaftColor = new Color(1.0f, 0.58f, 0.24f, 0.17f);
                        lightColor = new Color(1.0f, 0.60f, 0.28f, 1f);
                        lightIntensity = 2.45f;
                        break;

                    default: // Day (also smoothly reflects early morning sunrise tint!)
                        skyColor = new Color(0.72f, 0.90f, 1.0f, 1f);
                        emissionColor = new Color(0.62f, 0.85f, 1.0f, 1f) * 1.5f;
                        shaftColor = new Color(1.0f, 0.95f, 0.78f, 0.16f);
                        lightColor = new Color(1.0f, 0.95f, 0.84f, 1f);
                        lightIntensity = 2.95f;
                        break;
                }
            }

            float dt = immediate ? 1f : Mathf.Clamp01(Time.deltaTime * 2.5f);

            for (int i = 0; i < _glassMaterials.Count; i++)
            {
                Material m = _glassMaterials[i];
                if (m == null) continue;
                Color cur = Color.Lerp(m.color, skyColor, dt);
                m.color = cur;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", cur);
                if (m.HasProperty("_EmissionColor"))
                {
                    Color curEm = Color.Lerp(m.GetColor("_EmissionColor"), emissionColor, dt);
                    m.SetColor("_EmissionColor", curEm);
                }
            }

            for (int i = 0; i < _beamMaterials.Count; i++)
            {
                Material bm = _beamMaterials[i];
                if (bm == null) continue;
                Color cur = Color.Lerp(bm.color, shaftColor, dt);
                bm.color = cur;
                if (bm.HasProperty("_BaseColor")) bm.SetColor("_BaseColor", cur);
            }

            for (int i = 0; i < _windowLights.Count; i++)
            {
                Light l = _windowLights[i];
                if (l == null) continue;
                l.color = Color.Lerp(l.color, lightColor, dt);
                l.intensity = Mathf.Lerp(l.intensity, lightIntensity, dt);
            }
        }

        private static Material CreateTransparentBeamMaterial(Shader shader)
        {
            Material m = new Material(shader ?? Shader.Find("Standard"));
            m.color = new Color(1f, 0.95f, 0.78f, 0.15f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.color);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f); // URP Transparent
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            return m;
        }

        private static void CreateBox(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _glassMaterials.Count; i++) if (_glassMaterials[i] != null) Destroy(_glassMaterials[i]);
            for (int i = 0; i < _beamMaterials.Count; i++) if (_beamMaterials[i] != null) Destroy(_beamMaterials[i]);
            _glassMaterials.Clear();
            _beamMaterials.Clear();
            _windowLights.Clear();
        }
    }
}

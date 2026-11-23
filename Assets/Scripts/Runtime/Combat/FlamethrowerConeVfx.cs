using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// VFX เปลวไฟทรงกรวยสำหรับ Flamethrower (ลากเข้า PlayerPrefabs แล้วจัดวางตรงปลายกระบอกปืนได้เลย)
    /// PlayerController จะค้นหาและสั่งเปิด/ปิดอัตโนมัติเมื่อกดยิงปืนพ่นไฟ
    /// </summary>
    [ExecuteAlways]
    public class FlamethrowerConeVfx : MonoBehaviour
    {
        [Header("Cone Fire Settings (ปรับแต่งใน Inspector ได้ทันที)")]
        [SerializeField] private float coneAngle = 22f;
        [SerializeField] private float coneRadius = 0.12f;
        [SerializeField] private float flameSpeed = 11.5f;
        [SerializeField] private float flameLifetime = 0.45f;
        [SerializeField] private float emissionRate = 95f;
        [SerializeField] private float startSize = 0.42f;
        [SerializeField] private bool enableLight = true;
        [SerializeField] private float lightIntensity = 3.2f;
        [SerializeField] private float lightRange = 6.5f;

        private ParticleSystem _flamePs;
        private Light _flameLight;
        private bool _isFiring = false;
        private bool _isConfigured = false;

        private static Material _sharedFlameMaterial;
        private static Texture2D _sharedFlameTexture;

        private void Awake()
        {
            EnsureConfigured();
            if (Application.isPlaying && !_isFiring)
            {
                SetFiring(false, immediate: true);
            }
        }

        private void OnEnable()
        {
            EnsureConfigured();
            if (Application.isPlaying && !_isFiring)
            {
                SetFiring(false, immediate: true);
            }
        }

        private static Material GetOrCreateFlameMaterial()
        {
            if (_sharedFlameMaterial != null) return _sharedFlameMaterial;

            if (_sharedFlameTexture == null)
            {
                const int size = 64;
                _sharedFlameTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                _sharedFlameTexture.wrapMode = TextureWrapMode.Clamp;
                _sharedFlameTexture.filterMode = FilterMode.Bilinear;
                float half = (size - 1) * 0.5f;
                Color[] pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x - half) / half;
                        float dy = (y - half) / half;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        float alpha = Mathf.Clamp01(1f - dist);
                        alpha = alpha * alpha; // soft glowing fire puff falloff
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }
                _sharedFlameTexture.SetPixels(pixels);
                _sharedFlameTexture.Apply();
            }

            Shader s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Particles/Standard Unlit");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (s != null)
            {
                _sharedFlameMaterial = new Material(s);
                _sharedFlameMaterial.mainTexture = _sharedFlameTexture;
                _sharedFlameMaterial.color = Color.white;
            }
            return _sharedFlameMaterial;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _isConfigured = false;
            EnsureConfigured();
        }
#endif

        public void EnsureConfigured()
        {
            if (_isConfigured && _flamePs != null) return;

            _flamePs = GetComponent<ParticleSystem>();
            if (_flamePs == null)
            {
                _flamePs = gameObject.AddComponent<ParticleSystem>();
            }

            var main = _flamePs.main;
            main.loop = true;
            main.playOnAwake = !Application.isPlaying;
            main.duration = 1.0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(flameLifetime * 0.75f, flameLifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(flameSpeed * 0.85f, flameSpeed * 1.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(startSize * 0.7f, startSize * 1.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.92f, 0.45f, 0.95f),
                new Color(1f, 0.42f, 0.08f, 0.95f)
            );
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;

            var emission = _flamePs.emission;
            emission.enabled = !Application.isPlaying || _isFiring;
            emission.rateOverTime = emissionRate;

            var shape = _flamePs.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = coneAngle;
            shape.radius = coneRadius;
            shape.length = 0.25f;

            var colorOverLifetime = _flamePs.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.96f, 0.65f), 0.0f),
                    new GradientColorKey(new Color(1f, 0.52f, 0.08f), 0.38f),
                    new GradientColorKey(new Color(0.85f, 0.14f, 0.03f), 0.78f),
                    new GradientColorKey(new Color(0.22f, 0.08f, 0.05f), 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.95f, 0.12f),
                    new GradientAlphaKey(0.75f, 0.65f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

            var sizeOverLifetime = _flamePs.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0.0f, 0.45f);
            sizeCurve.AddKey(0.35f, 1.15f);
            sizeCurve.AddKey(1.0f, 1.65f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

            var psRenderer = GetComponent<ParticleSystemRenderer>();
            if (psRenderer != null)
            {
                psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                psRenderer.sortingOrder = 12;
                if (psRenderer.sharedMaterial == null || psRenderer.sharedMaterial.mainTexture == null)
                {
                    Material flameMat = GetOrCreateFlameMaterial();
                    if (flameMat != null)
                    {
                        psRenderer.sharedMaterial = flameMat;
                    }
                }
            }

            if (enableLight)
            {
                _flameLight = GetComponentInChildren<Light>(true);
                if (_flameLight == null)
                {
                    GameObject lightGo = new GameObject("FlameLight");
                    lightGo.transform.SetParent(transform, false);
                    lightGo.transform.localPosition = new Vector3(0f, 0.1f, 1.2f);
                    _flameLight = lightGo.AddComponent<Light>();
                    _flameLight.type = LightType.Point;
                    _flameLight.shadows = LightShadows.None;
                }
                _flameLight.color = new Color(1f, 0.5f, 0.12f, 1f);
                _flameLight.range = lightRange;
                _flameLight.intensity = lightIntensity;
                _flameLight.enabled = !Application.isPlaying || _isFiring;
            }

            _isConfigured = true;
        }

        public void UpdateAimTransform(Vector3 playerPos, Vector3 aimDir, float forwardOffset = 0.95f, float heightOffset = 0.35f)
        {
            Vector3 flatDir = aimDir;
            flatDir.y = 0f;
            if (flatDir.sqrMagnitude < 0.0001f) flatDir = Vector3.forward;
            flatDir.Normalize();

            transform.position = playerPos + flatDir * forwardOffset + Vector3.up * heightOffset;
            transform.rotation = Quaternion.LookRotation(flatDir, Vector3.up);
        }

        public void SetFiring(bool firing, bool immediate = false)
        {
            if (firing && !gameObject.activeSelf)
            {
                _isFiring = true;
                gameObject.SetActive(true);
            }

            EnsureConfigured();
            if (!immediate && _isFiring == firing && (!firing || (_flamePs != null && _flamePs.isPlaying && _flamePs.emission.enabled)))
            {
                return;
            }
            _isFiring = firing;

            if (_flamePs != null)
            {
                var emission = _flamePs.emission;
                emission.enabled = firing;
                if (firing)
                {
                    if (!_flamePs.isPlaying) _flamePs.Play(true);
                }
                else
                {
                    if (immediate)
                        _flamePs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    else
                        _flamePs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            if (_flameLight != null)
            {
                _flameLight.enabled = enableLight && firing;
            }
        }

        private void Update()
        {
            if (Application.isPlaying && _isFiring && _flameLight != null && enableLight)
            {
                _flameLight.intensity = lightIntensity * (0.85f + Mathf.PerlinNoise(Time.time * 18f, 0.5f) * 0.35f);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.45f, 0.08f, 0.75f);
            float dist = flameSpeed * flameLifetime;
            Vector3 origin = transform.position;
            Vector3 fwd = transform.forward;
            Vector3 right = transform.right;
            Vector3 up = transform.up;
            float endRadius = Mathf.Tan(coneAngle * Mathf.Deg2Rad) * dist + coneRadius;

            Gizmos.DrawLine(origin, origin + fwd * dist + right * endRadius);
            Gizmos.DrawLine(origin, origin + fwd * dist - right * endRadius);
            Gizmos.DrawLine(origin, origin + fwd * dist + up * endRadius);
            Gizmos.DrawLine(origin, origin + fwd * dist - up * endRadius);
            Gizmos.DrawWireSphere(origin + fwd * dist, endRadius * 0.5f);
        }
    }
}

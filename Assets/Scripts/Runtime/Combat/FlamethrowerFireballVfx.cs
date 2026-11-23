using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// VFX ลูกบอลเพลิงสำหรับสกิลคลิกขวาของ Legendary Flamethrower (Inferno Fireball Core)
    /// ทำงานร่วมกับ Projectile และ ObjectPoolManager (key: "FlamethrowerFireball")
    /// </summary>
    [ExecuteAlways]
    public class FlamethrowerFireballVfx : MonoBehaviour
    {
        [SerializeField] private float coreScale = 0.85f;
        [SerializeField] private float trailEmissionRate = 120f;
        [SerializeField] private float lightRange = 9f;
        [SerializeField] private float lightIntensity = 4.5f;

        private ParticleSystem _trailPs;
        private Light _fireLight;
        private Transform _coreSphere;
        private bool _configured = false;

        private static Material _sharedCoreMaterial;

        private static Material GetOrCreateCoreMaterial()
        {
            if (_sharedCoreMaterial != null) return _sharedCoreMaterial;
            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Unlit/Color");
            if (s != null)
            {
                _sharedCoreMaterial = new Material(s);
                Color coreColor = new Color(1f, 0.62f, 0.10f, 1f);
                _sharedCoreMaterial.color = coreColor;
                if (_sharedCoreMaterial.HasProperty("_BaseColor")) _sharedCoreMaterial.SetColor("_BaseColor", coreColor);
            }
            return _sharedCoreMaterial;
        }

        private void Awake()
        {
            EnsureConfigured();
        }

        private void OnEnable()
        {
            EnsureConfigured();
            if (_trailPs != null)
            {
                _trailPs.Clear();
                _trailPs.Play();
            }
            if (_fireLight != null) _fireLight.enabled = true;
        }

        private void OnDisable()
        {
            if (_trailPs != null) _trailPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_fireLight != null) _fireLight.enabled = false;
        }

        private void EnsureConfigured()
        {
            if (_configured && _trailPs != null) return;

            _coreSphere = transform.Find("FireballCore");
            if (_coreSphere == null)
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "FireballCore";
                sphere.transform.SetParent(transform, false);
                sphere.transform.localPosition = Vector3.zero;
                sphere.transform.localScale = Vector3.one * coreScale;
                Collider col = sphere.GetComponent<Collider>();
                if (col != null)
                {
                    if (Application.isPlaying) Destroy(col);
                    else DestroyImmediate(col);
                }

                _coreSphere = sphere.transform;
            }

            if (_coreSphere != null)
            {
                Renderer r = _coreSphere.GetComponent<Renderer>();
                if (r != null)
                {
                    Material coreMat = GetOrCreateCoreMaterial();
                    if (coreMat != null) r.sharedMaterial = coreMat;
                }
            }

            _trailPs = GetComponent<ParticleSystem>();
            if (_trailPs == null) _trailPs = gameObject.AddComponent<ParticleSystem>();

            var main = _trailPs.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.42f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.55f, 1.05f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.92f, 0.35f, 1f),
                new Color(1f, 0.28f, 0.05f, 1f)
            );
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 250;

            var emission = _trailPs.emission;
            emission.enabled = true;
            emission.rateOverTime = trailEmissionRate;

            var shape = _trailPs.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.38f;

            var colorOverLife = _trailPs.colorOverLifetime;
            colorOverLife.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.95f, 0.55f), 0f),
                    new GradientColorKey(new Color(1f, 0.45f, 0.06f), 0.45f),
                    new GradientColorKey(new Color(0.75f, 0.1f, 0.02f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.7f, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLife.color = new ParticleSystem.MinMaxGradient(g);

            var sizeOverLife = _trailPs.sizeOverLifetime;
            sizeOverLife.enabled = true;
            AnimationCurve sc = new AnimationCurve();
            sc.AddKey(0f, 1.0f);
            sc.AddKey(1f, 0.15f);
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sc);

            // ตั้ง Material เปลวไฟทรงกลมฟุ้ง (Sprites/Default) เพื่อไม่ให้ Particle เป็นสีชมพู (Missing Material) ใน URP
            ParticleSystemRenderer psRenderer = GetComponent<ParticleSystemRenderer>();
            if (psRenderer != null)
            {
                psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                psRenderer.sortingOrder = 14;
                Material flameMat = FlamethrowerConeVfx.GetOrCreateFlameMaterial();
                if (flameMat != null)
                {
                    psRenderer.sharedMaterial = flameMat;
                }
            }

            TrailRenderer tr = GetComponent<TrailRenderer>();
            if (tr != null && tr.sharedMaterial == null)
            {
                Shader trailShader = Shader.Find("Sprites/Default");
                if (trailShader != null) tr.sharedMaterial = new Material(trailShader);
            }

            _fireLight = GetComponentInChildren<Light>(true);
            if (_fireLight == null)
            {
                GameObject lgo = new GameObject("FireballLight");
                lgo.transform.SetParent(transform, false);
                _fireLight = lgo.AddComponent<Light>();
                _fireLight.type = LightType.Point;
                _fireLight.shadows = LightShadows.None;
            }
            _fireLight.color = new Color(1f, 0.42f, 0.08f, 1f);
            _fireLight.range = lightRange;
            _fireLight.intensity = lightIntensity;

            _configured = true;
        }

        private void Update()
        {
            if (Application.isPlaying && _coreSphere != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 24f) * 0.12f;
                _coreSphere.localScale = Vector3.one * (coreScale * pulse);
                _coreSphere.Rotate(Vector3.up * 360f * Time.deltaTime, Space.Self);
            }
        }
    }
}

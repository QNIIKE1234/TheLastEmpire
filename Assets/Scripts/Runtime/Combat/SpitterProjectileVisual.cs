using UnityEngine;

namespace TheLastEmpire
{
    public enum SpitProjectileStyle : byte
    {
        ZombieAcid  = 0, // Bright toxic neon green acid glob (ZombieSpitter & ZombieLeader Boss)
        AlienPlasma = 1  // Electric void-purple bio-plasma bolt (AlienSpitter)
    }

    /// <summary>
    /// Stylized 3D projectile visual for Spitter enemies (fully Object-Pooled via ObjectPoolManager):
    /// - ZombieAcid  : Toxic neon green acid glob + dark ink outline + orbiting acid beads + lime slime trail + green glow + pooled splash burst on impact
    /// - AlienPlasma : Electric purple plasma orb + bright magenta core + violet trail + plasma spark emitter + pooled purple flash burst on impact
    /// 100% procedural (zero external texture/material dependencies, PC & WebGL safe).
    /// </summary>
    public class SpitterProjectileVisual : MonoBehaviour
    {
        public const string ZombieAcidPoolKey   = "ZombieAcidSpit";
        public const string AlienPlasmaPoolKey  = "AlienBioPlasma";
        public const string ZombieSplashPoolKey = "ZombieAcidSplash";
        public const string AlienSplashPoolKey  = "AlienPlasmaBurst";

        [SerializeField] private SpitProjectileStyle _style;
        [SerializeField] private float _scaleMult = 1f;
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private Transform _orbitGroup;
        [SerializeField] private TrailRenderer _trailRenderer;
        [SerializeField] private ParticleSystem _trailParticles;
        [SerializeField] private Light _glowLight;
        [SerializeField] private SphereCollider _sphereCollider;

        private float _spinAge;
        private bool _armedForImpact;
        private bool _spawnedImpact;

        private static Texture2D _softGlowTex;
        private static Material _zombieCoreMat;
        private static Material _zombieOuterMat;
        private static Material _zombieOutlineMat;
        private static Material _zombieTrailMat;
        private static Material _zombieParticleMat;

        private static Material _alienCoreMat;
        private static Material _alienOuterMat;
        private static Material _alienOutlineMat;
        private static Material _alienTrailMat;
        private static Material _alienParticleMat;

        public static string GetProjectilePoolKey(SpitProjectileStyle style)
        {
            return style == SpitProjectileStyle.ZombieAcid ? ZombieAcidPoolKey : AlienPlasmaPoolKey;
        }

        public static string GetSplashPoolKey(SpitProjectileStyle style)
        {
            return style == SpitProjectileStyle.ZombieAcid ? ZombieSplashPoolKey : AlienSplashPoolKey;
        }

        /// <summary>
        /// Spawns a flat ground directional hitbox telegraph (using HitboxSpriteLibrary.DirectionalChevrons)
        /// along the Spitter's exact firing lane before the acid/plasma projectile launches.
        /// Auto-cleans up when duration ends or if the owner dies/disables early.
        /// </summary>
        public static GameObject SpawnSpitAimTelegraph(
            SpitProjectileStyle style,
            Vector3 origin,
            Vector3 direction,
            float length = 11.5f,
            float width = 1.05f,
            float duration = 0.48f,
            Transform ownerToWatch = null)
        {
            Sprite chevSp = HitboxSpriteLibrary.DirectionalChevrons;
            if (chevSp == null) return null;

            Vector3 flatDir = new Vector3(direction.x, 0f, direction.z);
            if (flatDir.sqrMagnitude < 0.0001f) flatDir = Vector3.forward;
            flatDir.Normalize();

            bool isZombie = style == SpitProjectileStyle.ZombieAcid;
            Color tint = isZombie
                ? new Color(0.35f, 1.0f, 0.25f, HitboxSpriteLibrary.DefaultHitboxAlpha)
                : new Color(0.84f, 0.28f, 1.0f, HitboxSpriteLibrary.DefaultHitboxAlpha);

            GameObject root = new GameObject(isZombie ? "ZombieSpitHitboxTelegraph" : "AlienSpitHitboxTelegraph");
            root.transform.position = new Vector3(origin.x, 0.06f, origin.z);

            const int segCount = 3;
            float segLen = Mathf.Max(1.6f, length / segCount);
            float bw = chevSp.bounds.size.x;
            float bh = chevSp.bounds.size.y;
            Quaternion rot = HitboxSpriteLibrary.GetGroundChevronRotation(flatDir);

            SpriteRenderer[] renderers = new SpriteRenderer[segCount];
            for (int i = 0; i < segCount; i++)
            {
                SpriteRenderer sr = HitboxSpriteLibrary.CreateGroundSpriteIndicator($"SpitLane_{i}", root.transform, chevSp, tint, 7);
                if (sr == null) continue;
                float distAlong = length * ((i + 0.5f) / segCount);
                sr.transform.position = new Vector3(origin.x + flatDir.x * distAlong, 0.06f, origin.z + flatDir.z * distAlong);
                sr.transform.rotation = rot;
                if (bw > 0.001f && bh > 0.001f)
                {
                    sr.transform.localScale = new Vector3(segLen / bw, width / bh, 1f);
                }
                renderers[i] = sr;
            }

            SpitterAimTelegraphRunner runner = root.AddComponent<SpitterAimTelegraphRunner>();
            runner.Init(renderers, tint, Mathf.Max(0.1f, duration), ownerToWatch);
            return root;
        }

        /// <summary>
        /// Pre-populates both Spitter projectile pools and both impact splash pools inside ObjectPoolManager.
        /// Safe to call repeatedly (idempotent) and automatically called on scene load / pool rebuild.
        /// </summary>
        public static void EnsurePoolsRegistered(ObjectPoolManager poolMgr)
        {
            if (poolMgr == null) return;
            EnsureSharedMaterials();

            if (!poolMgr.HasPool(ZombieAcidPoolKey))
            {
                GameObject tpl = CreateProjectileTemplate(SpitProjectileStyle.ZombieAcid, poolMgr.transform);
                poolMgr.CreatePool(ZombieAcidPoolKey, tpl, 16);
            }
            if (!poolMgr.HasPool(AlienPlasmaPoolKey))
            {
                GameObject tpl = CreateProjectileTemplate(SpitProjectileStyle.AlienPlasma, poolMgr.transform);
                poolMgr.CreatePool(AlienPlasmaPoolKey, tpl, 16);
            }
            if (!poolMgr.HasPool(ZombieSplashPoolKey))
            {
                GameObject tpl = CreateSplashTemplate(SpitProjectileStyle.ZombieAcid, poolMgr.transform);
                poolMgr.CreatePool(ZombieSplashPoolKey, tpl, 10);
            }
            if (!poolMgr.HasPool(AlienSplashPoolKey))
            {
                GameObject tpl = CreateSplashTemplate(SpitProjectileStyle.AlienPlasma, poolMgr.transform);
                poolMgr.CreatePool(AlienSplashPoolKey, tpl, 10);
            }
        }

        public static GameObject CreateSpitProjectile(
            SpitProjectileStyle style,
            Vector3 spawnPos,
            Vector3 direction,
            float speed,
            GameObject owner,
            float scaleMultiplier = 1f)
        {
            Vector3 flatDir = new Vector3(direction.x, 0f, direction.z);
            if (flatDir.sqrMagnitude < 0.0001f) flatDir = Vector3.forward;
            flatDir.Normalize();

            Quaternion rot = Quaternion.LookRotation(flatDir, Vector3.up);
            string poolKey = GetProjectilePoolKey(style);
            GameObject go = null;

            if (ObjectPoolManager.Instance != null)
            {
                EnsurePoolsRegistered(ObjectPoolManager.Instance);
                go = ObjectPoolManager.Instance.SpawnFromPool(poolKey, spawnPos, rot);
            }

            if (go == null)
            {
                // Fallback if ObjectPoolManager is unavailable in an isolated test scene
                go = CreateProjectileTemplate(style, null);
                go.transform.position = spawnPos;
                go.transform.rotation = rot;
                go.SetActive(true);
            }

            SpitterProjectileVisual visual = go.GetComponent<SpitterProjectileVisual>();
            Projectile proj = go.GetComponent<Projectile>();
            if (proj != null)
            {
                proj.PoolKey = ObjectPoolManager.Instance != null ? poolKey : "";
                float resolvedSpeed = Projectile.ResolvePistolProjectileSpeed(speed);
                proj.SetSpeed(resolvedSpeed);
                Rigidbody projRb = go.GetComponent<Rigidbody>();
                if (projRb != null)
                {
                    projRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                }
                proj.Setup(flatDir, spawnPos + flatDir * 14f, owner);
                proj.SetEnemyProjectile(true);
            }

            if (visual != null)
            {
                visual.ArmForFlight(style, scaleMultiplier, proj);
            }

            return go;
        }

        private static GameObject CreateProjectileTemplate(SpitProjectileStyle style, Transform poolParent)
        {
            string poolKey = GetProjectilePoolKey(style);
            GameObject go = new GameObject($"{poolKey}_Template");
            go.SetActive(false);
            if (poolParent != null)
            {
                go.transform.SetParent(poolParent, false);
            }

            SphereCollider col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.34f;

            SpitterProjectileVisual visual = go.AddComponent<SpitterProjectileVisual>();
            visual._sphereCollider = col;
            visual.BuildVisual(style, 1f);

            Projectile proj = go.AddComponent<Projectile>();
            proj.PoolKey = poolKey;
            bool isZombie = style == SpitProjectileStyle.ZombieAcid;
            proj.SetBulletLight(
                isZombie ? new Color(0.38f, 1.0f, 0.18f) : new Color(0.76f, 0.22f, 1.0f),
                4.2f,
                isZombie ? 2.4f : 2.8f);

            Rigidbody projRb = go.GetComponent<Rigidbody>();
            if (projRb != null)
            {
                projRb.useGravity = false;
                projRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            return go;
        }

        private static GameObject CreateSplashTemplate(SpitProjectileStyle style, Transform poolParent)
        {
            EnsureSharedMaterials();
            string splashKey = GetSplashPoolKey(style);
            bool isZombie = style == SpitProjectileStyle.ZombieAcid;
            Material splashMat = isZombie ? _zombieOuterMat : _alienOuterMat;

            GameObject burst = new GameObject($"{splashKey}_Template");
            burst.SetActive(false);
            if (poolParent != null)
            {
                burst.transform.SetParent(poolParent, false);
            }

            SpitterSplashBurst comp = burst.AddComponent<SpitterSplashBurst>();
            Transform[] drops = new Transform[SpitterSplashBurst.DropCount];
            for (int i = 0; i < SpitterSplashBurst.DropCount; i++)
            {
                GameObject d = CreateNoColliderSphere($"Splash_{i}", burst.transform, splashMat);
                d.transform.localScale = Vector3.one * 0.18f;
                drops[i] = d.transform;
            }
            comp.ConfigureTemplate(splashKey, drops);
            return burst;
        }

        private void ArmForFlight(SpitProjectileStyle style, float scaleMultiplier, Projectile proj)
        {
            _style = style;
            _scaleMult = Mathf.Clamp(scaleMultiplier, 0.75f, 2.0f);
            _spinAge = 0f;
            _spawnedImpact = false;
            _armedForImpact = true;

            bool isZombie = style == SpitProjectileStyle.ZombieAcid;

            if (_visualRoot != null)
            {
                _visualRoot.localScale = Vector3.one * _scaleMult;
            }
            if (_orbitGroup != null)
            {
                _orbitGroup.localRotation = Quaternion.identity;
            }
            if (_sphereCollider == null) _sphereCollider = GetComponent<SphereCollider>();
            if (_sphereCollider != null)
            {
                _sphereCollider.radius = 0.34f * Mathf.Clamp(_scaleMult, 0.75f, 1.8f);
            }

            if (_trailRenderer == null) _trailRenderer = GetComponent<TrailRenderer>();
            if (_trailRenderer != null)
            {
                _trailRenderer.Clear();
                _trailRenderer.time = isZombie ? 0.20f : 0.24f;
                _trailRenderer.startWidth = (isZombie ? 0.36f : 0.34f) * _scaleMult;
                _trailRenderer.endWidth = 0.03f;
                _trailRenderer.enabled = true;
            }

            if (_trailParticles != null)
            {
                var main = _trailParticles.main;
                main.startSize = new ParticleSystem.MinMaxCurve(0.10f * _scaleMult, 0.20f * _scaleMult);
                _trailParticles.Clear(true);
                _trailParticles.Play(true);
            }

            Color glowCol = isZombie ? new Color(0.38f, 1.0f, 0.18f) : new Color(0.76f, 0.22f, 1.0f);
            float glowRange = 4.2f * _scaleMult;
            float glowIntensity = isZombie ? 2.4f : 2.8f;
            if (proj != null)
            {
                proj.SetBulletLight(glowCol, glowRange, glowIntensity);
            }
            if (_glowLight != null)
            {
                _glowLight.color = glowCol;
                _glowLight.range = glowRange;
                _glowLight.intensity = glowIntensity;
                _glowLight.enabled = true;
            }
        }

        private void BuildVisual(SpitProjectileStyle style, float scaleMultiplier)
        {
            _style = style;
            _scaleMult = Mathf.Clamp(scaleMultiplier, 0.75f, 2.0f);
            EnsureSharedMaterials();

            bool isZombie = style == SpitProjectileStyle.ZombieAcid;
            Material coreMat = isZombie ? _zombieCoreMat : _alienCoreMat;
            Material outerMat = isZombie ? _zombieOuterMat : _alienOuterMat;
            Material outlineMat = isZombie ? _zombieOutlineMat : _alienOutlineMat;
            Material trailMat = isZombie ? _zombieTrailMat : _alienTrailMat;
            Material particleMat = isZombie ? _zombieParticleMat : _alienParticleMat;

            _visualRoot = new GameObject("SpitVisualRoot").transform;
            _visualRoot.SetParent(transform, false);
            _visualRoot.localScale = Vector3.one * _scaleMult;

            // 1. Elongated 3D inner bright core (teardrop/plasma bolt shape oriented along +Z travel direction)
            GameObject core = CreateNoColliderSphere("Core", _visualRoot, coreMat);
            core.transform.localScale = isZombie
                ? new Vector3(0.30f, 0.26f, 0.50f)
                : new Vector3(0.24f, 0.24f, 0.52f);

            // 2. Translucent glowing outer energy / acid membrane
            GameObject outer = CreateNoColliderSphere("OuterGlow", _visualRoot, outerMat);
            outer.transform.localScale = isZombie
                ? new Vector3(0.46f, 0.40f, 0.68f)
                : new Vector3(0.44f, 0.42f, 0.72f);

            // 3. Crisp cartoon ink dark rim shell
            GameObject rim = CreateNoColliderSphere("InkCoreRim", _visualRoot, outlineMat);
            rim.transform.localPosition = new Vector3(0f, -0.05f, -0.04f);
            rim.transform.localScale = isZombie
                ? new Vector3(0.35f, 0.24f, 0.54f)
                : new Vector3(0.32f, 0.22f, 0.56f);

            // 4. Orbiting droplets / plasma sparks around the main glob
            _orbitGroup = new GameObject("OrbitDroplets").transform;
            _orbitGroup.SetParent(_visualRoot, false);
            int dropCount = isZombie ? 3 : 2;
            for (int i = 0; i < dropCount; i++)
            {
                float ang = (i * Mathf.PI * 2f) / dropCount;
                GameObject drop = CreateNoColliderSphere($"Droplet_{i}", _orbitGroup, isZombie ? coreMat : outerMat);
                drop.transform.localPosition = new Vector3(Mathf.Cos(ang) * 0.24f, Mathf.Sin(ang) * 0.14f, -0.18f - i * 0.08f);
                drop.transform.localScale = Vector3.one * (isZombie ? 0.12f : 0.11f);
            }

            // 5. Glowing TrailRenderer
            _trailRenderer = gameObject.AddComponent<TrailRenderer>();
            _trailRenderer.time = isZombie ? 0.20f : 0.24f;
            _trailRenderer.minVertexDistance = 0.08f;
            _trailRenderer.startWidth = (isZombie ? 0.36f : 0.34f) * _scaleMult;
            _trailRenderer.endWidth = 0.03f;
            _trailRenderer.autodestruct = false;
            _trailRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _trailRenderer.receiveShadows = false;
            _trailRenderer.sharedMaterial = trailMat;

            Gradient grad = new Gradient();
            if (isZombie)
            {
                grad.SetKeys(
                    new[]
                    {
                        new GradientColorKey(new Color(0.55f, 1.0f, 0.20f), 0f),
                        new GradientColorKey(new Color(0.18f, 0.85f, 0.12f), 0.55f),
                        new GradientColorKey(new Color(0.08f, 0.45f, 0.06f), 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(0.90f, 0f),
                        new GradientAlphaKey(0.50f, 0.55f),
                        new GradientAlphaKey(0.0f, 1f)
                    });
            }
            else
            {
                grad.SetKeys(
                    new[]
                    {
                        new GradientColorKey(new Color(0.95f, 0.55f, 1.0f), 0f),
                        new GradientColorKey(new Color(0.68f, 0.14f, 1.0f), 0.50f),
                        new GradientColorKey(new Color(0.30f, 0.04f, 0.62f), 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(0.92f, 0f),
                        new GradientAlphaKey(0.55f, 0.50f),
                        new GradientAlphaKey(0.0f, 1f)
                    });
            }
            _trailRenderer.colorGradient = grad;

            // 6. Lightweight Shedding Particle Emitter (Acid drips / Purple plasma sparks)
            GameObject psObj = new GameObject("SpitTrailParticles");
            psObj.transform.SetParent(_visualRoot, false);
            _trailParticles = psObj.AddComponent<ParticleSystem>();
            var main = _trailParticles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.28f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.10f * _scaleMult, 0.20f * _scaleMult);
            main.startColor = isZombie
                ? new Color(0.48f, 1.0f, 0.22f, 0.88f)
                : new Color(0.84f, 0.28f, 1.0f, 0.90f);
            main.maxParticles = 28;

            var em = _trailParticles.emission;
            em.enabled = true;
            em.rateOverTime = 45f;

            var sh = _trailParticles.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.14f;

            var sz = _trailParticles.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

            var ren = _trailParticles.GetComponent<ParticleSystemRenderer>();
            ren.renderMode = ParticleSystemRenderMode.Billboard;
            ren.sharedMaterial = particleMat;
            ren.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ren.receiveShadows = false;

            // 7. Subtle Colored Point Light (pops beautifully at Dusk & Night!)
            GameObject lightObj = new GameObject("SpitGlowLight");
            lightObj.transform.SetParent(_visualRoot, false);
            _glowLight = lightObj.AddComponent<Light>();
            _glowLight.type = LightType.Point;
            _glowLight.color = isZombie ? new Color(0.38f, 1.0f, 0.18f) : new Color(0.76f, 0.22f, 1.0f);
            _glowLight.range = 4.2f * _scaleMult;
            _glowLight.intensity = isZombie ? 2.4f : 2.8f;
            _glowLight.shadows = LightShadows.None;
        }

        private void Update()
        {
            _spinAge += Time.deltaTime;
            if (_orbitGroup != null)
            {
                _orbitGroup.Rotate(0f, 0f, 460f * Time.deltaTime, Space.Self);
            }
            if (_visualRoot != null)
            {
                float pulse = 1f + Mathf.Sin(_spinAge * 24f) * 0.08f;
                _visualRoot.localScale = new Vector3(_scaleMult * pulse, _scaleMult * (2f - pulse), _scaleMult);
            }
        }

        private void OnDisable()
        {
            if (_trailParticles != null)
            {
                _trailParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (_trailRenderer != null)
            {
                _trailRenderer.Clear();
            }
            TrySpawnImpactBurst();
        }

        private void OnDestroy()
        {
            TrySpawnImpactBurst();
        }

        private void TrySpawnImpactBurst()
        {
            if (!_armedForImpact || _spawnedImpact || !Application.isPlaying) return;
            _armedForImpact = false;
            _spawnedImpact = true;

            if (ObjectPoolManager.Instance == null) return;

            string splashKey = GetSplashPoolKey(_style);
            EnsurePoolsRegistered(ObjectPoolManager.Instance);
            GameObject burstObj = ObjectPoolManager.Instance.SpawnFromPool(splashKey, transform.position, Quaternion.identity);
            if (burstObj != null)
            {
                SpitterSplashBurst burst = burstObj.GetComponent<SpitterSplashBurst>();
                if (burst != null)
                {
                    burst.PlayBurst(_scaleMult);
                }
            }
        }

        private static GameObject CreateNoColliderSphere(string name, Transform parent, Material mat)
        {
            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = name;
            Collider c = s.GetComponent<Collider>();
            if (c != null) Destroy(c);
            s.transform.SetParent(parent, false);
            Renderer r = s.GetComponent<Renderer>();
            if (r != null)
            {
                if (mat != null) r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            return s;
        }

        private static void EnsureSharedMaterials()
        {
            if (_softGlowTex == null)
            {
                const int sz = 32;
                _softGlowTex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
                _softGlowTex.wrapMode = TextureWrapMode.Clamp;
                Color[] px = new Color[sz * sz];
                float half = (sz - 1) * 0.5f;
                for (int y = 0; y < sz; y++)
                {
                    for (int x = 0; x < sz; x++)
                    {
                        float dx = (x - half) / half;
                        float dy = (y - half) / half;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - r);
                        px[y * sz + x] = new Color(1f, 1f, 1f, a * a);
                    }
                }
                _softGlowTex.SetPixels(px);
                _softGlowTex.Apply();
            }

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Sprites/Default");

            if (_zombieCoreMat == null) _zombieCoreMat = CreateUnlitMat(unlit, new Color(0.62f, 1.0f, 0.20f, 1f), transparent: false);
            if (_zombieOuterMat == null) _zombieOuterMat = CreateUnlitMat(unlit, new Color(0.24f, 0.95f, 0.14f, 0.45f), transparent: true);
            if (_zombieOutlineMat == null) _zombieOutlineMat = CreateUnlitMat(unlit, new Color(0.06f, 0.16f, 0.04f, 1f), transparent: false);
            if (_zombieTrailMat == null) _zombieTrailMat = CreateParticleMat(particleShader, null, Color.white);
            if (_zombieParticleMat == null) _zombieParticleMat = CreateParticleMat(particleShader, _softGlowTex, new Color(0.50f, 1.0f, 0.22f, 0.92f));

            if (_alienCoreMat == null) _alienCoreMat = CreateUnlitMat(unlit, new Color(0.95f, 0.72f, 1.0f, 1f), transparent: false);
            if (_alienOuterMat == null) _alienOuterMat = CreateUnlitMat(unlit, new Color(0.72f, 0.14f, 1.0f, 0.48f), transparent: true);
            if (_alienOutlineMat == null) _alienOutlineMat = CreateUnlitMat(unlit, new Color(0.14f, 0.03f, 0.24f, 1f), transparent: false);
            if (_alienTrailMat == null) _alienTrailMat = CreateParticleMat(particleShader, null, Color.white);
            if (_alienParticleMat == null) _alienParticleMat = CreateParticleMat(particleShader, _softGlowTex, new Color(0.82f, 0.28f, 1.0f, 0.92f));
        }

        private static Material CreateUnlitMat(Shader shader, Color color, bool transparent)
        {
            Material m = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            if (transparent)
            {
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
                if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            return m;
        }

        private static Material CreateParticleMat(Shader shader, Texture2D tex, Color color)
        {
            Material m = CreateUnlitMat(shader, color, transparent: true);
            if (tex != null)
            {
                m.mainTexture = tex;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            }
            return m;
        }
    }

    /// <summary>
    /// Pooled 5-droplet impact splash burst for SpitterProjectileVisual (auto-returns to ObjectPoolManager after 0.24s).
    /// </summary>
    public class SpitterSplashBurst : MonoBehaviour
    {
        public const int DropCount = 5;
        private const float BurstDuration = 0.24f;

        [SerializeField] private string _poolKey;
        [SerializeField] private Transform[] _drops;

        private readonly Vector3[] _velocities = new Vector3[DropCount];
        private float _elapsed;
        private float _scaleMult = 1f;
        private bool _playing;

        public void ConfigureTemplate(string poolKey, Transform[] drops)
        {
            _poolKey = poolKey;
            _drops = drops;
        }

        public void PlayBurst(float scaleMult)
        {
            _scaleMult = Mathf.Clamp(scaleMult, 0.75f, 2.0f);
            _elapsed = 0f;
            _playing = true;

            if (_drops == null) return;
            int count = Mathf.Min(_drops.Length, DropCount);
            for (int i = 0; i < count; i++)
            {
                if (_drops[i] == null) continue;
                _drops[i].localPosition = Vector3.zero;
                _drops[i].localScale = Vector3.one * (0.18f * _scaleMult);
                float ang = (i * Mathf.PI * 2f) / count + Random.Range(-0.3f, 0.3f);
                _velocities[i] = new Vector3(
                    Mathf.Cos(ang) * Random.Range(2.0f, 3.8f),
                    Random.Range(0.8f, 2.2f),
                    Mathf.Sin(ang) * Random.Range(2.0f, 3.8f));
            }
        }

        private void Update()
        {
            if (!_playing) return;

            _elapsed += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(_elapsed / BurstDuration);
            if (_drops != null)
            {
                int count = Mathf.Min(_drops.Length, DropCount);
                for (int i = 0; i < count; i++)
                {
                    if (_drops[i] == null) continue;
                    _velocities[i].y -= 9.5f * Time.deltaTime;
                    _drops[i].localPosition += _velocities[i] * Time.deltaTime;
                    _drops[i].localScale = Vector3.one * (0.20f * _scaleMult * k);
                }
            }

            if (_elapsed >= BurstDuration)
            {
                _playing = false;
                if (!string.IsNullOrEmpty(_poolKey) && ObjectPoolManager.Instance != null)
                {
                    ObjectPoolManager.Instance.ReturnToPool(_poolKey, gameObject);
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }
    }

    /// <summary>
    /// Handles smooth pulse + automatic cleanup for Spitter ground hitbox telegraph lanes.
    /// </summary>
    public class SpitterAimTelegraphRunner : MonoBehaviour
    {
        private SpriteRenderer[] _renderers;
        private Color _baseColor;
        private float _duration;
        private float _elapsed;
        private Transform _ownerToWatch;
        private Health _ownerHealth;

        public void Init(SpriteRenderer[] renderers, Color baseColor, float duration, Transform ownerToWatch)
        {
            _renderers = renderers;
            _baseColor = baseColor;
            _duration = duration;
            _elapsed = 0f;
            _ownerToWatch = ownerToWatch;
            _ownerHealth = ownerToWatch != null ? ownerToWatch.GetComponent<Health>() : null;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            bool ownerInvalid = (_ownerToWatch != null && !_ownerToWatch.gameObject.activeInHierarchy) ||
                                (_ownerHealth != null && _ownerHealth.IsDead);
            if (ownerInvalid || _elapsed >= _duration)
            {
                Destroy(gameObject);
                return;
            }

            if (_renderers != null)
            {
                float progress = Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, _duration));
                float pulse = 0.85f + 0.35f * Mathf.Sin(progress * Mathf.PI * 3f);
                Color c = _baseColor;
                c.a = Mathf.Clamp01(_baseColor.a * pulse);
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] != null) _renderers[i].color = c;
                }
            }
        }
    }
}

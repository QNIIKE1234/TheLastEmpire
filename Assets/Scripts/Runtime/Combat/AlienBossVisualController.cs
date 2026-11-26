using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TheLastEmpire.Network;

namespace TheLastEmpire
{
    /// <summary>
    /// Live visual animation & VFX controller for the Floating Fungal Cyclops Alien Boss (AlienLeader).
    /// Layers procedural life on top of the rigged FBX animations (Idle, Walk, Run, Vomit, Slash1, Death):
    /// 1. Weightless floating levitation & directional banking tilt + ground hover shadow disc.
    /// 2. Giant Cyclops EyeBall bone live darting & player-tracking in LateUpdate.
    /// 3. Animated 8x back tentacles responsive trailing wave in LateUpdate.
    /// 4. Wide-open Jaw Vomit Spew VFX (acidic violet-magenta globule torrent + mouth glow)
    ///    synchronized with minion vomit launches on both Host and Co-op Clients!
    /// </summary>
    public class AlienBossVisualController : MonoBehaviour
    {
        [Header("Floating Settings")]
        [SerializeField] private float hoverBobAmplitude = 0.12f;
        [SerializeField] private float hoverBobFrequency = 2.0f;
        [SerializeField] private float moveTiltAngle = 14f;

        [Header("Eye Rolling & Tracking")]
        [SerializeField] private float maxEyeYaw = 28f;
        [SerializeField] private float maxEyePitch = 20f;
        [SerializeField] private float eyeDartSpeed = 10f;

        private Transform _modelRoot;
        private Transform _bodyBone;
        private Transform _eyeBone;
        private Transform _jawBone;
        private readonly List<Transform[]> _tentacleChains = new List<Transform[]>();

        private Rigidbody _rb;
        private BaseEnemyAI _enemyAI;
        private Health _health;

        private Vector3 _baseModelLocalPos;
        private bool _hasBaseModelPos;
        private float _phaseOffset;

        // Live Eye darting state
        private float _eyeTargetYaw;
        private float _eyeTargetPitch;
        private float _eyeCurrentYaw;
        private float _eyeCurrentPitch;
        private float _eyeDartTimer;
        private bool _eyeTrackPlayer = true;

        // Vomit sequence state
        private bool _isVomiting;
        private float _vomitJawExtraAngle;
        private float _clientVomitCooldown;

        // Hover shadow disc beneath the floating orb
        private GameObject _hoverShadow;
        private Material _hoverShadowMat;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _enemyAI = GetComponent<BaseEnemyAI>();
            _health = GetComponent<Health>();
            _phaseOffset = Random.Range(0f, 10f);
        }

        private void Start()
        {
            CacheRigBones();
            CreateHoverShadowDisc();
        }

        private void OnDestroy()
        {
            if (_hoverShadowMat != null)
            {
                Destroy(_hoverShadowMat);
                _hoverShadowMat = null;
            }
            if (_hoverShadow != null)
            {
                Destroy(_hoverShadow);
                _hoverShadow = null;
            }
        }

        private void CacheRigBones()
        {
            _modelRoot = transform.Find("Alien_Leader_Rigged_Model");
            if (_modelRoot == null)
            {
                Animator anim = GetComponentInChildren<Animator>(true);
                if (anim != null && anim.transform != transform)
                {
                    _modelRoot = anim.transform;
                }
            }

            if (_modelRoot != null)
            {
                _baseModelLocalPos = _modelRoot.localPosition;
                _hasBaseModelPos = true;
            }

            _bodyBone = FindChildRecursive(transform, "Body");
            _eyeBone = FindChildRecursive(transform, "EyeBall");
            _jawBone = FindChildRecursive(transform, "JawBot");

            _tentacleChains.Clear();
            for (int t = 0; t < 8; t++)
            {
                Transform b0 = FindChildRecursive(transform, $"Tent_{t}_0");
                Transform b1 = FindChildRecursive(transform, $"Tent_{t}_1");
                Transform b2 = FindChildRecursive(transform, $"Tent_{t}_2");
                Transform b3 = FindChildRecursive(transform, $"Tent_{t}_3");
                if (b0 != null && b1 != null && b2 != null && b3 != null)
                {
                    _tentacleChains.Add(new[] { b0, b1, b2, b3 });
                }
            }
        }

        private void CreateHoverShadowDisc()
        {
            if (_hoverShadow != null) return;

            _hoverShadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _hoverShadow.name = "AlienBossHoverShadow";
            Collider c = _hoverShadow.GetComponent<Collider>();
            if (c != null) Destroy(c);

            _hoverShadow.transform.SetParent(transform, false);
            _hoverShadow.transform.localPosition = new Vector3(0f, -0.96f, 0f);
            _hoverShadow.transform.localScale = new Vector3(1.65f, 0.015f, 1.65f);

            Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (sh != null)
            {
                _hoverShadowMat = new Material(sh);
                Color sc = new Color(0.10f, 0.02f, 0.14f, 0.45f);
                if (_hoverShadowMat.HasProperty("_Surface")) _hoverShadowMat.SetFloat("_Surface", 1f);
                if (_hoverShadowMat.HasProperty("_SrcBlend")) _hoverShadowMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (_hoverShadowMat.HasProperty("_DstBlend")) _hoverShadowMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (_hoverShadowMat.HasProperty("_ZWrite")) _hoverShadowMat.SetFloat("_ZWrite", 0f);
                _hoverShadowMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _hoverShadowMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                _hoverShadowMat.color = sc;
                if (_hoverShadowMat.HasProperty("_BaseColor")) _hoverShadowMat.SetColor("_BaseColor", sc);

                Renderer r = _hoverShadow.GetComponent<Renderer>();
                if (r != null)
                {
                    r.sharedMaterial = _hoverShadowMat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
            }
        }

        private void Update()
        {
            if (_health != null && _health.IsDead)
            {
                if (_hoverShadow != null && _hoverShadow.activeSelf) _hoverShadow.SetActive(false);
                return;
            }

            if (_eyeBone == null) CacheRigBones();

            if (_clientVomitCooldown > 0f) _clientVomitCooldown -= Time.deltaTime;

            // On Co-op Clients: when Host enters Telegraph state during vomit-summon, mirror the vomit animation & VFX!
            if (EmpireNetworkManager.IsOnlineClient &&
                _enemyAI != null &&
                _enemyAI.CurrentState == BaseEnemyAI.AIState.Telegraph &&
                !_isVomiting &&
                _clientVomitCooldown <= 0f)
            {
                _clientVomitCooldown = 3.0f;
                StartCoroutine(PlayClientVomitVfxOnlyRoutine());
            }

            UpdateEyeTargetDecision();
        }

        private void LateUpdate()
        {
            if (_health != null && _health.IsDead) return;

            float t = Time.time + _phaseOffset;

            // 1. Ethereal floating hover bob & velocity banking tilt (replaces leg walking!)
            Vector3 horizVel = _rb != null ? new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z) : Vector3.zero;
            float speed = horizVel.magnitude;

            if (_modelRoot != null && _hasBaseModelPos)
            {
                float bob = Mathf.Sin(t * hoverBobFrequency) * hoverBobAmplitude;
                float sway = Mathf.Cos(t * (hoverBobFrequency * 0.65f)) * 0.05f;
                _modelRoot.localPosition = _baseModelLocalPos + new Vector3(sway, bob, 0f);

                // Pitch gently forward when gliding fast, roll slightly on strafes
                Vector3 localVel = transform.InverseTransformDirection(horizVel);
                float targetPitch = Mathf.Clamp(localVel.z / 3.5f, -1f, 1f) * moveTiltAngle;
                float targetRoll = -Mathf.Clamp(localVel.x / 3.5f, -1f, 1f) * (moveTiltAngle * 0.75f);
                Quaternion desiredLocalRot = Quaternion.Euler(targetPitch, 0f, targetRoll);
                _modelRoot.localRotation = Quaternion.Slerp(_modelRoot.localRotation, desiredLocalRot, Time.deltaTime * 6f);

                // Keep hover shadow pinned cleanly on the ground below the bobbing orb
                if (_hoverShadow != null)
                {
                    float groundScale = 1.65f - bob * 0.65f;
                    _hoverShadow.transform.localScale = new Vector3(groundScale, 0.015f, groundScale);
                    Vector3 wp = transform.position;
                    _hoverShadow.transform.position = new Vector3(wp.x, 0.03f, wp.z);
                    _hoverShadow.transform.rotation = Quaternion.identity;
                }
            }

            // 2. Live Giant Cyclops Eye rolling / darting + player tracking on top of animation!
            if (_eyeBone != null)
            {
                _eyeCurrentYaw = Mathf.Lerp(_eyeCurrentYaw, _eyeTargetYaw, Time.deltaTime * eyeDartSpeed);
                _eyeCurrentPitch = Mathf.Lerp(_eyeCurrentPitch, _eyeTargetPitch, Time.deltaTime * eyeDartSpeed);
                _eyeBone.localRotation *= Quaternion.Euler(_eyeCurrentPitch, 0f, _eyeCurrentYaw);
            }

            // 3. Extra jaw flare during intense vomit heave
            if (_jawBone != null && _vomitJawExtraAngle > 0.1f)
            {
                _jawBone.localRotation *= Quaternion.Euler(-_vomitJawExtraAngle, 0f, 0f);
            }

            // 4. Subtle velocity-responsive secondary wave on all 8 back tentacles
            if (_tentacleChains.Count > 0)
            {
                float waveBoost = _isVomiting ? 1.85f : (1.0f + Mathf.Clamp01(speed / 3.5f) * 0.55f);
                float freq = _isVomiting ? 7.5f : (3.2f + Mathf.Clamp01(speed / 3.5f) * 2.2f);
                for (int ti = 0; ti < _tentacleChains.Count; ti++)
                {
                    Transform[] chain = _tentacleChains[ti];
                    float phase = ti * 0.85f;
                    float side = (ti % 2 == 0) ? -1f : 1f;
                    for (int s = 0; s < chain.Length; s++)
                    {
                        if (chain[s] == null) continue;
                        float segFactor = (0.35f + s * 0.45f) * waveBoost;
                        float dyaw = Mathf.Sin(t * freq - s * 0.9f + phase) * 5.5f * segFactor * side;
                        float dpitch = Mathf.Cos(t * freq * 0.85f - s * 0.8f + phase) * 4.0f * segFactor;
                        chain[s].localRotation *= Quaternion.Euler(dpitch, 0f, dyaw);
                    }
                }
            }
        }

        private void UpdateEyeTargetDecision()
        {
            if (_isVomiting)
            {
                // Eye darts wildly during vomit summon!
                _eyeTargetYaw = Mathf.Sin(Time.time * 16f) * 22f;
                _eyeTargetPitch = Mathf.Cos(Time.time * 12f) * 16f;
                return;
            }

            _eyeDartTimer -= Time.deltaTime;
            if (_eyeDartTimer <= 0f)
            {
                // Alternate between intense player stare & eerie rapid eye-darting around the arena!
                _eyeTrackPlayer = Random.value < 0.55f;
                if (_eyeTrackPlayer)
                {
                    _eyeDartTimer = Random.Range(1.2f, 2.6f);
                }
                else
                {
                    _eyeDartTimer = Random.Range(0.45f, 1.15f);
                    _eyeTargetYaw = Random.Range(-maxEyeYaw, maxEyeYaw);
                    _eyeTargetPitch = Random.Range(-maxEyePitch * 0.85f, maxEyePitch * 0.85f);
                }
            }

            if (_eyeTrackPlayer)
            {
                PlayerController target = PlayerRegistry.GetClosestAlivePlayer(transform.position) ?? PlayerController.Instance;
                if (target != null)
                {
                    Vector3 toTarget = (target.transform.position + Vector3.up * 0.7f) - GetMouthWorldPosition();
                    Vector3 localDir = transform.InverseTransformDirection(toTarget.normalized);
                    float yaw = Mathf.Atan2(localDir.x, Mathf.Max(0.2f, localDir.z)) * Mathf.Rad2Deg;
                    float pitch = -Mathf.Asin(Mathf.Clamp(localDir.y, -0.95f, 0.95f)) * Mathf.Rad2Deg;
                    _eyeTargetYaw = Mathf.Clamp(-yaw * 0.65f, -maxEyeYaw, maxEyeYaw);
                    _eyeTargetPitch = Mathf.Clamp(pitch * 0.55f, -maxEyePitch, maxEyePitch);
                }
            }
        }

        public Vector3 GetMouthWorldPosition()
        {
            if (_jawBone != null)
            {
                return _jawBone.position + transform.forward * 0.55f + Vector3.up * 0.12f;
            }
            return transform.position + transform.forward * 0.95f + Vector3.up * 1.15f;
        }

        /// <summary>
        /// Opens jaw wide, plays Vomit animation, spews a glowing violet-magenta bio-vomit stream,
        /// and calls onSpewPeak right when the jaw gapes open to launch 2-3 Alien minions out of the mouth!
        /// </summary>
        public Coroutine TriggerVomitSummonSequence(System.Action<Vector3, Vector3> onSpewPeak)
        {
            if (_isVomiting) return null;
            return StartCoroutine(VomitSummonRoutine(onSpewPeak));
        }

        private IEnumerator VomitSummonRoutine(System.Action<Vector3, Vector3> onSpewPeak)
        {
            _isVomiting = true;

            // Face closest player before vomiting aliens toward them
            PlayerController target = PlayerRegistry.GetClosestAlivePlayer(transform.position) ?? PlayerController.Instance;
            if (target != null)
            {
                Vector3 toP = target.transform.position - transform.position;
                toP.y = 0f;
                if (toP.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(toP.normalized);
                }
            }

            if (_enemyAI != null)
            {
                _enemyAI.PlayActionAnimation("Vomit", 1.55f, 0.06f);
            }

            // 1. Inhale / windup (0.38s)
            float windup = 0f;
            while (windup < 0.38f)
            {
                if (_health != null && _health.IsDead) { FinishVomitCleanup(); yield break; }
                windup += Time.deltaTime;
                _vomitJawExtraAngle = Mathf.Lerp(0f, 10f, windup / 0.38f);
                yield return null;
            }

            // 2. Jaw gapes MASSIVELY WIDE OPEN -> Spew bio-vomit torrent & vomit out Alien minions!
            Vector3 mouthPos = GetMouthWorldPosition();
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();

            StartCoroutine(SpawnBioVomitTorrentVfx(mouthPos, fwd, 0.85f));
            onSpewPeak?.Invoke(mouthPos, fwd);

            float spewElapsed = 0f;
            while (spewElapsed < 0.82f)
            {
                if (_health != null && _health.IsDead) { FinishVomitCleanup(); yield break; }
                spewElapsed += Time.deltaTime;
                float heave = Mathf.Abs(Mathf.Sin(spewElapsed * 14f));
                _vomitJawExtraAngle = 12f + heave * 10f;
                yield return null;
            }

            // 3. Recovery close
            float rec = 0f;
            while (rec < 0.32f)
            {
                rec += Time.deltaTime;
                _vomitJawExtraAngle = Mathf.Lerp(14f, 0f, rec / 0.32f);
                yield return null;
            }

            FinishVomitCleanup();
        }

        private IEnumerator PlayClientVomitVfxOnlyRoutine()
        {
            _isVomiting = true;
            if (_enemyAI != null)
            {
                _enemyAI.PlayActionAnimation("Vomit", 1.55f, 0.06f);
            }
            yield return new WaitForSeconds(0.38f);
            if (_health == null || !_health.IsDead)
            {
                Vector3 fwd = transform.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                StartCoroutine(SpawnBioVomitTorrentVfx(GetMouthWorldPosition(), fwd.normalized, 0.85f));
            }
            yield return new WaitForSeconds(1.1f);
            FinishVomitCleanup();
        }

        private void FinishVomitCleanup()
        {
            _vomitJawExtraAngle = 0f;
            _isVomiting = false;
        }

        /// <summary>
        /// Spawns a dramatic glowing magenta-lime alien vomit torrent (bursting globules + mouth light + ground splat puddle).
        /// </summary>
        private IEnumerator SpawnBioVomitTorrentVfx(Vector3 startMouthPos, Vector3 forwardDir, float duration)
        {
            GameObject lightObj = new GameObject("AlienVomitMouthLight");
            lightObj.transform.position = startMouthPos;
            Light mouthLight = lightObj.AddComponent<Light>();
            mouthLight.type = LightType.Point;
            mouthLight.color = new Color(0.95f, 0.22f, 0.78f);
            mouthLight.range = 6.5f;
            mouthLight.intensity = 5.5f;
            mouthLight.shadows = LightShadows.None;

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Material vomitMatA = unlit != null ? new Material(unlit) : null;
            Material vomitMatB = unlit != null ? new Material(unlit) : null;
            if (vomitMatA != null)
            {
                Color cA = new Color(0.95f, 0.16f, 0.68f, 0.92f);
                vomitMatA.color = cA;
                if (vomitMatA.HasProperty("_BaseColor")) vomitMatA.SetColor("_BaseColor", cA);
            }
            if (vomitMatB != null)
            {
                Color cB = new Color(0.58f, 0.96f, 0.26f, 0.92f);
                vomitMatB.color = cB;
                if (vomitMatB.HasProperty("_BaseColor")) vomitMatB.SetColor("_BaseColor", cB);
            }

            List<(GameObject go, Vector3 vel, float life, float maxLife)> globs = new List<(GameObject, Vector3, float, float)>(24);
            float emitTimer = 0f;
            float elapsed = 0f;

            while (elapsed < duration || globs.Count > 0)
            {
                float dt = Time.deltaTime;
                elapsed += dt;

                Vector3 liveMouth = GetMouthWorldPosition();
                if (lightObj != null)
                {
                    lightObj.transform.position = liveMouth;
                    mouthLight.intensity = elapsed < duration ? (4.5f + Mathf.Sin(elapsed * 22f) * 1.5f) : Mathf.Max(0f, mouthLight.intensity - dt * 12f);
                }

                // Emit thick bio-vomit globules while jaw is gaping open!
                if (elapsed < duration)
                {
                    emitTimer -= dt;
                    if (emitTimer <= 0f)
                    {
                        emitTimer = 0.045f;
                        for (int k = 0; k < 2; k++)
                        {
                            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                            g.name = "BioVomitGlob";
                            Collider gc = g.GetComponent<Collider>();
                            if (gc != null) Destroy(gc);

                            g.transform.position = liveMouth + Random.insideUnitSphere * 0.14f;
                            float sz = Random.Range(0.14f, 0.30f);
                            g.transform.localScale = new Vector3(sz, sz * 0.85f, sz * 1.35f);

                            Renderer gr = g.GetComponent<Renderer>();
                            if (gr != null)
                            {
                                gr.sharedMaterial = (Random.value < 0.65f ? vomitMatA : vomitMatB);
                                gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                            }

                            Vector3 right = Vector3.Cross(Vector3.up, forwardDir).normalized;
                            Vector3 vel = forwardDir * Random.Range(3.2f, 6.2f)
                                        + right * Random.Range(-1.8f, 1.8f)
                                        + Vector3.up * Random.Range(0.4f, 2.2f);
                            globs.Add((g, vel, 0f, Random.Range(0.45f, 0.72f)));
                        }
                    }
                }

                // Update arcing globules with gravity + ground flatten splat
                for (int i = globs.Count - 1; i >= 0; i--)
                {
                    var item = globs[i];
                    item.life += dt;
                    if (item.go == null || item.life >= item.maxLife)
                    {
                        if (item.go != null) Destroy(item.go);
                        globs.RemoveAt(i);
                        continue;
                    }

                    Vector3 p = item.go.transform.position + item.vel * dt;
                    item.vel += Vector3.down * 11.5f * dt;
                    if (p.y <= 0.06f)
                    {
                        p.y = 0.06f;
                        item.vel *= 0.25f;
                        item.vel.y = 0f;
                        // Flatten into an oozing vomit splat on the ground
                        Vector3 s = item.go.transform.localScale;
                        item.go.transform.localScale = Vector3.Lerp(s, new Vector3(s.x * 1.65f, 0.03f, s.z * 1.65f), dt * 10f);
                    }
                    item.go.transform.position = p;
                    globs[i] = item;
                }

                yield return null;
            }

            if (lightObj != null) Destroy(lightObj);
            if (vomitMatA != null) Destroy(vomitMatA);
            if (vomitMatB != null) Destroy(vomitMatB);
        }

        private static Transform FindChildRecursive(Transform root, string exactName)
        {
            if (root == null) return null;
            if (root.name == exactName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), exactName);
                if (found != null) return found;
            }
            return null;
        }
    }
}

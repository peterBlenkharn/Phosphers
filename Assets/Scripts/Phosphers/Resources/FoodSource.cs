using System.Collections.Generic;
using UnityEngine;
using Phosphers.Core;
using Phosphers.Fog;

namespace Phosphers.Resources
{
    public class FoodSource : MonoBehaviour
    {
        [Header("Activation (fog-gated)")]
        [Tooltip("How often to poll Fog.HasSeen while inactive.")]
        [SerializeField] private float fogPollInterval = 0.2f;
        [SerializeField] private float activationDelay = 0.0f;

        [Header("Emission timing (fuzzy, seeded)")]
        [SerializeField] private float emitIntervalMean = 2.0f;
        [SerializeField] private float emitIntervalJitter = 0.4f; // fraction of mean (0.4 = ±40%)
        [SerializeField] private bool deterministic = true;
        [SerializeField] private int seedBase = 12345;

        [Header("Placement (annulus/sector)")]
        [SerializeField] private float minRadius = 1.0f;
        [SerializeField] private float maxRadius = 2.5f;
        [SerializeField] private bool useSector = false;
        [SerializeField, Range(-180f, 180f)] private float sectorCentreDeg = 0f;
        [SerializeField, Range(0f, 180f)] private float sectorHalfAngle = 45f;

        [Header("Spacing & concurrency")]
        [SerializeField] private float minDistanceFromExistingBit = 0.6f;
        [SerializeField] private int maxSpawnAttempts = 10;
        [SerializeField] private int maxConcurrentNearby = 3;

        [Header("Lifetime / depletion")]
        [SerializeField] private int totalBitsToEmit = -1;   // -1 = unlimited
        [SerializeField] private float lifetimeSeconds = -1f;  // -1 = unlimited
        [SerializeField] private bool destroyOnDepleted = true;

        [Header("Bit payload")]
        [SerializeField] private BitType bitType = BitType.Generic;
        [SerializeField] private int bitValue = 1;
        [SerializeField] private float bitWeight = 1f;
        [SerializeField] private Vector2 bitRotationRandomDeg = new Vector2(0f, 360f);

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        // Events
        public event System.Action<FoodSource> OnDiscovered;
        public event System.Action<FoodSource> OnDepleted;
        public event System.Action<Bit> OnBitEmitted;

        // Runtime
        private bool _discovered;
        private bool _active;
        private float _fogPollTimer;
        private float _activateCountdown;
        private float _emitTimer;
        private float _lifeTimer;
        private int _emittedCount;
        private System.Random _rng;

        private readonly HashSet<Bit> _myBits = new HashSet<Bit>(); // for concurrent cap

        private void OnEnable()
        {
            _discovered = false;
            _active = false;
            _fogPollTimer = 0f;
            _activateCountdown = activationDelay;
            _emitTimer = 0f;
            _lifeTimer = 0f;
            _emittedCount = 0;

            var rs = ResourceSystem.Instance;
            if (rs != null)
            {
                rs.OnBitDespawned += HandleBitDespawned; // track concurrent count
            }

            // Deterministic seed that’s stable per-instance
            int seed = seedBase;
            if (deterministic) seed ^= gameObject.GetInstanceID();
            _rng = new System.Random(seed);
        }

        private void OnDisable()
        {
            var rs = ResourceSystem.Instance;
            if (rs != null)
            {
                rs.OnBitDespawned -= HandleBitDespawned;
            }
            _myBits.Clear();
        }

        private void HandleBitDespawned(Bit bit)
        {
            if (bit != null && bit.Owner == this) _myBits.Remove(bit);
        }

        private void Update()
        {
            var fog = FogSystem.Instance;
            var rs = ResourceSystem.Instance;
            if (rs == null) return;

            float dt = Time.deltaTime;

            // 1) Discovery gate
            if (!_discovered)
            {
                _fogPollTimer -= dt;
                if (_fogPollTimer <= 0f)
                {
                    _fogPollTimer = Mathf.Max(0.05f, fogPollInterval);
                    if (fog == null || fog.HasSeen(transform.position))
                    {
                        _discovered = true;
                        OnDiscovered?.Invoke(this);
                    }
                }
                return; // wait until discovered
            }

            // 2) Activation delay
            if (!_active)
            {
                _activateCountdown -= dt;
                if (_activateCountdown <= 0f) _active = true;
                else return;
            }

            // 3) Depletion check
            if (lifetimeSeconds > 0f)
            {
                _lifeTimer += dt;
                if (_lifeTimer >= lifetimeSeconds) { Deplete(); return; }
            }
            if (totalBitsToEmit >= 0 && _emittedCount >= totalBitsToEmit) { Deplete(); return; }

            // 4) Concurrent cap
            if (_myBits.Count >= maxConcurrentNearby) return;

            // 5) Emit on a jittered cadence
            _emitTimer -= dt;
            if (_emitTimer <= 0f)
            {
                if (TrySpawnBit(rs))
                {
                    _emittedCount++;
                }
                // schedule next
                float mean = Mathf.Max(0.02f, emitIntervalMean);
                float frac = Mathf.Clamp01(emitIntervalJitter);
                float jitter = (float)(_rng.NextDouble() * 2.0 - 1.0) * frac;
                _emitTimer = Mathf.Max(0.02f, mean * (1f + jitter));
            }
        }

        private void Deplete()
        {
            _active = false;
            OnDepleted?.Invoke(this);
            if (destroyOnDepleted) Destroy(gameObject);
        }

        private bool TrySpawnBit(ResourceSystem rs)
        {
            // Find a valid position: annulus (and sector if enabled), with spacing
            Vector2 pos;
            bool found = false;

            for (int attempt = 0; attempt < Mathf.Max(1, maxSpawnAttempts); attempt++)
            {
                float r = SampleRadius(minRadius, maxRadius, _rng);
                float angDeg = useSector
                    ? SampleSectorAngle(sectorCentreDeg, sectorHalfAngle, _rng)
                    : (float)(_rng.NextDouble() * 360.0 - 180.0);

                float rad = angDeg * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                pos = (Vector2)transform.position + dir * r;

                if (HasSpacing(rs, pos, minDistanceFromExistingBit))
                {
                    found = true;
                    // spawn
                    var spec = new BitSpec(bitType, bitValue, bitWeight);
                    var bit = rs.SpawnBit(spec, pos);
                    if (bit == null) return false; // pool exhausted
                    bit.Owner = this;
                    bit.ApplyRandomRotation(_rng, bitRotationRandomDeg);
                    _myBits.Add(bit);
                    OnBitEmitted?.Invoke(bit);
                    return true;
                }
            }

            return false;
        }

        private static float SampleRadius(float rMin, float rMax, System.Random rng)
        {
            rMin = Mathf.Max(0f, rMin);
            rMax = Mathf.Max(rMin + 1e-4f, rMax);
            // Uniform over area: r = sqrt(lerp(rMin^2, rMax^2, u))
            float u = (float)rng.NextDouble();
            return Mathf.Sqrt(Mathf.Lerp(rMin * rMin, rMax * rMax, u));
        }

        private static float SampleSectorAngle(float centreDeg, float halfDeg, System.Random rng)
        {
            float u = (float)rng.NextDouble() * 2f - 1f; // [-1,1]
            return centreDeg + u * halfDeg;
        }

        private static bool HasSpacing(ResourceSystem rs, Vector2 pos, float minDist)
        {
            float minDist2 = minDist * minDist;
            foreach (var b in rs.ActiveBits)
            {
                if (!b.IsAvailable) continue;
                Vector2 d = b.Position - pos;
                if (d.sqrMagnitude < minDist2) return false;
            }
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            Color col = _active ? new Color(0.2f, 1f, 0.4f, 0.9f) :
                         _discovered ? new Color(1f, 0.9f, 0.3f, 0.9f) :
                                       new Color(0.5f, 0.5f, 0.5f, 0.9f);

            Gizmos.color = col;
            // Annulus
            DrawCircle(transform.position, minRadius);
            DrawCircle(transform.position, maxRadius);

            // Sector
            if (useSector)
            {
                float c = sectorCentreDeg, h = sectorHalfAngle;
                DrawRayDeg(c - h, maxRadius);
                DrawRayDeg(c + h, maxRadius);
            }

            void DrawCircle(Vector3 cpos, float r)
            {
                const int N = 48;
                Vector3 prev = cpos + new Vector3(r, 0, 0);
                for (int i = 1; i <= N; i++)
                {
                    float a = i * (Mathf.PI * 2f / N);
                    Vector3 p = cpos + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0);
                    Gizmos.DrawLine(prev, p);
                    prev = p;
                }
            }
            void DrawRayDeg(float deg, float len)
            {
                float rad = deg * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0);
                Gizmos.DrawLine(transform.position, transform.position + dir * len);
            }
        }
    }
}

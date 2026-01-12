using System.Collections.Generic;
using UnityEngine;
using Phosphers.Agents;
using Phosphers.Resources;
using Phosphers.World; // for Anchor
using Phosphers.Core.Pooling;

namespace Phosphers.Core
{
    [DefaultExecutionOrder(-200)]
    public class ResourceSystem : MonoBehaviour
    {
        public static ResourceSystem Instance { get; private set; }

        [Header("Bit Pool")]
        [SerializeField] private Bit bitPrefab;
        [SerializeField, Min(0)] private int initialPoolSize = 64;
        [SerializeField] private Transform worldParent; // active bits parent
        [SerializeField] private Transform poolParent;  // inactive bits parent
        [SerializeField] private bool logPoolWarnings = true;

        [Header("Score")]
        public int Score { get; private set; }
        public event System.Action<int> OnScoreChanged;
        public event System.Action<Bit> OnBitSpawned;    // NEW
        public event System.Action<Bit> OnBitDespawned;  // NEW

        // NEW: fires once per successful rim deposit
        public event System.Action<Phosphers.Agents.Phospher, Phosphers.Resources.BitSpec> OnBitDeposited; // NEW

        // Runtime
        private ObjectPool<Bit> _pool;
        public IReadOnlyCollection<Bit> ActiveBits => _pool != null ? _pool.Active : System.Array.Empty<Bit>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (bitPrefab == null)
            {
                Debug.LogError("[ResourceSystem] Bit prefab not assigned."); enabled = false; return;
            }
            if (worldParent == null)
            {
                var go = new GameObject("Bits.World");
                worldParent = go.transform;
            }
            if (poolParent == null)
            {
                var go = new GameObject("Bits.Pool");
                poolParent = go.transform;
            }

            _pool = new ObjectPool<Bit>(bitPrefab, initialPoolSize, worldParent, poolParent, logPoolWarnings);
        }

        public void ResetScore()
        {
            Score = 0;
            OnScoreChanged?.Invoke(Score);
        }

        public void WarmPool(int count)
        {
            _pool?.Warm(count);
        }

        public Bit SpawnBit(BitSpec spec, Vector2 position, float zRotDeg = 0f)
        {
            if (_pool == null) return null;

            var bit = _pool.Get(position, Quaternion.Euler(0, 0, zRotDeg));
            if (bit == null) return null;

            bit.Configure(spec);
            bit.MarkAvailable(true);
            OnBitSpawned?.Invoke(bit);                       // NEW
            Debug.Log($"[RS] SpawnBit @ {position} (active={ActiveBits.Count})"); // DIAG
            return bit;
        }

        public void DespawnBit(Bit bit)
        {
            if (bit == null) return;

            if (_pool == null) return;

            bit.MarkAvailable(false);
            if (_pool.Release(bit))
            {
                OnBitDespawned?.Invoke(bit);                     // NEW
                bit.Owner = null;                                // clear link
                Debug.Log("[RS] DespawnBit"); // DIAG
            }
        }

        public void DespawnAllActive()
        {
            if (_pool == null || ActiveBits.Count == 0) return;
            var tmp = new List<Bit>(ActiveBits);
            foreach (var bit in tmp)
            {
                if (bit != null) DespawnBit(bit);
            }
        }

        /// <summary>Called by Phospher when it deposits at the Anchor rim.</summary>
        public void OnDeposit(Anchor anchor, Phospher p, BitSpec spec)
        {
            Score += spec.value;
            OnScoreChanged?.Invoke(Score);
            OnBitDeposited?.Invoke(p, spec);
            Debug.Log($"[RS] Deposit +{spec.value} ? Score={Score}"); // DIAG
            // (VFX/SFX later)
        }
    }
}

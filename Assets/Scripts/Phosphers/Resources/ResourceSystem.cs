using System.Collections.Generic;
using UnityEngine;
using Phosphers.Agents;
using Phosphers.Resources;
using Phosphers.World; // for Anchor

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
        private readonly Queue<Bit> _pool = new Queue<Bit>();
        private readonly HashSet<Bit> _active = new HashSet<Bit>();
        public IReadOnlyCollection<Bit> ActiveBits => _active;

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

            WarmPool(initialPoolSize);
        }

        public void ResetScore()
        {
            Score = 0;
            OnScoreChanged?.Invoke(Score);
        }

        public void WarmPool(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var b = Instantiate(bitPrefab, poolParent);
                b.gameObject.SetActive(false);
                b.MarkAvailable(false);
                _pool.Enqueue(b);
            }
        }

        public Bit SpawnBit(BitSpec spec, Vector2 position, float zRotDeg = 0f)
        {
            if (_pool.Count == 0)
            {
                if (logPoolWarnings) Debug.LogWarning("[ResourceSystem] Bit pool exhausted; consider raising initialPoolSize.");
                return null; // gracefully skip spawn
            }

            var bit = _pool.Dequeue();
            _active.Add(bit);

            bit.transform.SetParent(worldParent, false);
            bit.transform.position = position;
            bit.transform.rotation = Quaternion.Euler(0, 0, zRotDeg);

            bit.Configure(spec);
            bit.MarkAvailable(true);

            bit.gameObject.SetActive(true);
            OnBitSpawned?.Invoke(bit);                       // NEW
            Debug.Log($"[RS] SpawnBit @ {position} (active={_active.Count})"); // DIAG
            return bit;
        }

        public void DespawnBit(Bit bit)
        {
            if (bit == null) return;

            // If it was active, retire it
            if (_active.Remove(bit))
            {
                bit.MarkAvailable(false);
                bit.gameObject.SetActive(false);
                bit.transform.SetParent(poolParent, false);
                _pool.Enqueue(bit);
                OnBitDespawned?.Invoke(bit);                     // NEW
                bit.Owner = null;                                // clear link
                Debug.Log($"[RS] DespawnBit (pool={_pool.Count})"); // DIAG
            }
            else
            {
                // Already pooled or unknown; ignore quietly
            }
        }

        public void DespawnAllActive()
        {
            // snapshot because DespawnBit mutates _active
            if (_active.Count == 0) return;
            var tmp = System.Array.Empty<Bit>();
            if (tmp.Length < _active.Count) tmp = new Bit[_active.Count];
            _active.CopyTo(tmp);
            foreach (var b in tmp)
            {
                if (b != null) DespawnBit(b);
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

using UnityEngine;
using Phosphers.Core;
using Phosphers.Resources;
using Phosphers.World;

namespace Phosphers.Core
{
    [DefaultExecutionOrder(-50)]
    public class RunStats : MonoBehaviour
    {
        public static RunStats Instance { get; private set; }

        [Header("Debug")]
        [SerializeField] private bool logEvents = false;

        // Per-run counters
        public int BitsSpawnedThisRun { get; private set; }
        public int BitsPickedUpThisRun { get; private set; }   // proxy: despawns
        public int BitsDepositedThisRun { get; private set; }
        public int SourcesDiscoveredThisRun { get; private set; }
        public int SourcesDepletedThisRun { get; private set; }

        public float RunElapsed { get; private set; }

        // Lifecycle
        private bool _isRunActive;
        private float _runStartTime;

        private GameManager _gm;
        private ResourceSystem _rs;

        private bool _rsHooked;

        [Header("Inspector (live view)")]
        [SerializeField] private float _inspectorElapsed;
        [SerializeField] private int _inspectorScore, _inspectorSpawned, _inspectorPicked, _inspectorDeposited, _inspectorDisc, _inspectorDepl;


        // Snapshot type for the summary panel
        public struct Snapshot
        {
            public int score;
            public float elapsed;
            public int bitsSpawned, bitsPickedUp, bitsDeposited;
            public int sourcesDiscovered, sourcesDepleted;
        }

        public Snapshot GetSnapshot()
        {
            return new Snapshot
            {
                score = _rs != null ? _rs.Score : 0,
                elapsed = RunElapsed,
                bitsSpawned = BitsSpawnedThisRun,
                bitsPickedUp = BitsPickedUpThisRun,
                bitsDeposited = BitsDepositedThisRun,
                sourcesDiscovered = SourcesDiscoveredThisRun,
                sourcesDepleted = SourcesDepletedThisRun
            };
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
#if UNITY_2023_1_OR_NEWER
            _gm = FindFirstObjectByType<GameManager>();
#else
            _gm = FindObjectOfType<GameManager>();
#endif
            _rs = ResourceSystem.Instance;

            if (_gm != null) _gm.OnStateChanged += HandleGameState;

            if (!_rsHooked) StartCoroutine(TryHookResourceSystem());
            HookAllFoodSources(true);

            


        }

        private System.Collections.IEnumerator TryHookResourceSystem()
        {
            // Wait until the singleton exists, then hook once
            while (ResourceSystem.Instance == null) yield return null;
            if (!_rsHooked)
            {
                _rs = ResourceSystem.Instance;
                // Ensure a clean single subscription
                HookResourceSystem(false);
                HookResourceSystem(true);
                _rsHooked = true;
                if (logEvents) Debug.Log("[RunStats] Hooked ResourceSystem after wait.");
            }
        }

        private void OnDisable()
        {
            if (_gm != null) _gm.OnStateChanged -= HandleGameState;
            HookResourceSystem(false);
            HookAllFoodSources(false);
        }

        private void Update()
        {
            if (_isRunActive) RunElapsed = Time.time - _runStartTime;

            // Mirror to inspector (Play Mode)
            if (Application.isPlaying)
            {
                var rs = _rs ?? ResourceSystem.Instance;
                _inspectorElapsed = RunElapsed;
                _inspectorScore = rs != null ? rs.Score : 0;
                _inspectorSpawned = BitsSpawnedThisRun;
                _inspectorPicked = BitsPickedUpThisRun;
                _inspectorDeposited = BitsDepositedThisRun;
                _inspectorDisc = SourcesDiscoveredThisRun;
                _inspectorDepl = SourcesDepletedThisRun;
            }
        }

        private void HandleGameState(GameState s)
        {
            Debug.Log("[RunStats] GameState = " + s);
            switch (s)
            {
                case GameState.Run:
                    BeginRun();
                    break;
                case GameState.End:
                case GameState.Menu:
                    EndRun();
                    break;
            }
        }

        private void BeginRun()
        {
            BitsSpawnedThisRun = 0;
            BitsPickedUpThisRun = 0;
            BitsDepositedThisRun = 0;
            SourcesDiscoveredThisRun = 0;
            SourcesDepletedThisRun = 0;

            _runStartTime = Time.time;
            RunElapsed = 0f;
            _isRunActive = true;

            // Re-hook sources that might have (de)spawned since last time
            HookAllFoodSources(false);
            HookAllFoodSources(true);

            if (logEvents) Debug.Log("[RunStats] Run started.");
        }

        private void EndRun()
        {
            if (!_isRunActive) return;
            _isRunActive = false;
            RunElapsed = Time.time - _runStartTime;
            if (logEvents)
            {
                var s = GetSnapshot();
                Debug.Log($"[RunStats] Run ended. Score={s.score} Time={s.elapsed:F1}s " +
                          $"Spawned={s.bitsSpawned} PickedUp={s.bitsPickedUp} Deposited={s.bitsDeposited} " +
                          $"Sources: Disc={s.sourcesDiscovered} Depl={s.sourcesDepleted}");
            }
        }

        private void HookResourceSystem(bool hook)
        {
            var inst = ResourceSystem.Instance;
            if (inst == null) return;

            // Always remove first to avoid duplicate handlers
            inst.OnBitSpawned -= HandleBitSpawned;
            inst.OnBitDespawned -= HandleBitDespawned;
            inst.OnBitDeposited -= HandleBitDeposited;

            if (hook)
            {
                inst.OnBitSpawned += HandleBitSpawned;
                inst.OnBitDespawned += HandleBitDespawned;
                inst.OnBitDeposited += HandleBitDeposited;
                _rsHooked = true;
            }
            else
            {
                _rsHooked = false;
            }
        }

        private void HookAllFoodSources(bool hook)
        {
#if UNITY_2023_1_OR_NEWER
            var sources = FindObjectsByType<FoodSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var sources = FindObjectsOfType<FoodSource>(true);
#endif
            foreach (var fs in sources)
            {
                if (hook)
                {
                    fs.OnDiscovered += HandleSourceDiscovered;
                    fs.OnDepleted += HandleSourceDepleted;
                }
                else
                {
                    fs.OnDiscovered -= HandleSourceDiscovered;
                    fs.OnDepleted -= HandleSourceDepleted;
                }
            }
        }

        // --- Event handlers ---
        private void HandleBitSpawned(Bit b)
        {
            if (!_isRunActive) return;
            BitsSpawnedThisRun++;
        }

        private void HandleBitDespawned(Bit b)
        {
            if (!_isRunActive) return;
            // In current gameplay, despawn == pickup (pool returns on pickup)
            BitsPickedUpThisRun++;
        }

        private void HandleBitDeposited(Phosphers.Agents.Phospher p, BitSpec spec)
        {
            if (!_isRunActive) return;
            BitsDepositedThisRun++;
        }

        private void HandleSourceDiscovered(FoodSource fs)
        {
            if (!_isRunActive) return;
            SourcesDiscoveredThisRun++;
        }

        private void HandleSourceDepleted(FoodSource fs)
        {
            if (!_isRunActive) return;
            SourcesDepletedThisRun++;
        }
    }
}

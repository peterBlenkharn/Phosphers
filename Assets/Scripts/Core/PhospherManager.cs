using System.Collections.Generic;
using UnityEngine;
using Phosphers.Agents;
using Phosphers.Signals;

namespace Phosphers.Core
{
    public class PhospherManager : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private MonoBehaviour vectorFieldProvider; // must implement IVectorField
        [SerializeField] private Phospher phospherPrefab;
        [SerializeField] private PhospherSettings settings;

        private IVectorField Field => vectorFieldProvider as IVectorField;

        [Header("Spawn")]
        public int count = 12;

        [Header("Spawn Timing")]
        [SerializeField] private bool spawnOverTime = true;
        [SerializeField] private float spawnInterval = 0.15f;
        [SerializeField] private Vector2 spawnIntervalJitter = new Vector2(0.0f, 0.10f); // +/- jitter

        [Header("Spawn Geometry")]
        public float marginFromAnchor = 0.2f;  // extra beyond ring outer edge
        public float spawnAnnulusMin = 1.5f;
        public float spawnAnnulusMax = 3.0f;
        public bool useSector = true;
        [Range(-180f, 180f)] public float sectorCentreDeg = 0f;
        [Range(0f, 180f)] public float sectorHalfAngle = 45f;
        public float spawnSpacing = 0.8f;
        public int spawnMaxAttemptsPerAgent = 20;

        private Coroutine _spawnRoutine;

        private readonly List<Phospher> _alive = new();

        private void Reset()
        {
#if UNITY_2023_1_OR_NEWER
            if (gameManager == null) gameManager = Object.FindFirstObjectByType<GameManager>();
#else
            if (gameManager == null) gameManager = Object.FindObjectOfType<GameManager>();
#endif
        }

        private void OnEnable()
        {
            if (gameManager == null) Reset();
            if (gameManager != null) gameManager.OnStateChanged += HandleState;
        }

        private void OnDisable()
        {
            if (gameManager != null) gameManager.OnStateChanged -= HandleState;
            if (_spawnRoutine != null) StopCoroutine(_spawnRoutine);
            DespawnAll();
        }


        private void HandleState(GameState s)
        {
            if (s == GameState.Run) SpawnAll();
            else DespawnAll();
        }

        private void SpawnAll()
        {
            DespawnAll();
            if (phospherPrefab == null || settings == null || gameManager.AnchorTransform == null) return;

            if (_spawnRoutine != null) StopCoroutine(_spawnRoutine);
            _spawnRoutine = StartCoroutine(SpawnCo(gameManager.AnchorTransform));
        }


        private System.Collections.IEnumerator SpawnCo(Transform anchor)
        {
            // Get true ring radius
            var a = anchor.GetComponent<Phosphers.World.Anchor>();
            float ring = a != null ? a.OuterRadius : 1.0f;

            for (int i = 0; i < count; i++)
            {
                Vector3 pos = FindSpawnPoint(anchor.position, ring);
                var p = Instantiate(phospherPrefab, pos, Quaternion.identity);
                p.Init(this, anchor, Field, settings);
                Register(p);

                if (spawnOverTime)
                {
                    float jitter = Random.Range(-spawnIntervalJitter.x, spawnIntervalJitter.y);
                    yield return new WaitForSeconds(Mathf.Max(0.01f, spawnInterval + jitter));
                }
            }
        }
        private Vector3 FindSpawnPoint(Vector3 centre, float ringOuter)
        {
            float minR = Mathf.Max(ringOuter + marginFromAnchor, spawnAnnulusMin);
            float maxR = Mathf.Max(minR + 0.01f, spawnAnnulusMax);

            for (int attempt = 0; attempt < spawnMaxAttemptsPerAgent; attempt++)
            {
                float r = Random.Range(minR, maxR);
                float angDeg = useSector
                    ? Random.Range(sectorCentreDeg - sectorHalfAngle, sectorCentreDeg + sectorHalfAngle)
                    : Random.Range(-180f, 180f);
                float ang = angDeg * Mathf.Deg2Rad;
                Vector2 offset = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                Vector3 candidate = centre + (Vector3)offset;

                bool clear = true;
                foreach (var a in _alive)
                {
                    if (a == null) continue;
                    if (((Vector2)candidate - (Vector2)a.transform.position).sqrMagnitude < spawnSpacing * spawnSpacing)
                    {
                        clear = false; break;
                    }
                }
                if (clear) return candidate;
            }
            // Fallback: on minR, random angle
            float fallbackAng = Random.Range(-Mathf.PI, Mathf.PI);
            return centre + (Vector3)(new Vector2(Mathf.Cos(fallbackAng), Mathf.Sin(fallbackAng)) * minR);
        }


        public void Register(Phospher p)
        {
            if (!_alive.Contains(p)) _alive.Add(p);
        }

        public void Unregister(Phospher p)
        {
            _alive.Remove(p);
        }

        public List<Phospher> GetNeighbours(Phospher requester, float radius)
        {
            float r2 = radius * radius;
            var list = new List<Phospher>();
            Vector2 pos = requester.transform.position;
            foreach (var p in _alive)
            {
                if (p == requester || p == null) continue;
                if (((Vector2)p.transform.position - pos).sqrMagnitude <= r2)
                    list.Add(p);
            }
            return list;
        }

        private void DespawnAll()
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
                if (_alive[i] != null) Destroy(_alive[i].gameObject);
            _alive.Clear();
        }
    }
}

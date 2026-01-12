using UnityEngine;
using Phosphers.Core;

namespace Phosphers.Signals
{
    public class TrailDrawController : MonoBehaviour
    {
        [SerializeField] private TrailField trailField;
        [SerializeField] private float drawRadius = 0.8f;
        [SerializeField] private float drawStrength = 0.6f;
        [SerializeField] private float drawInterval = 0.02f;
        [SerializeField] private float juicePerSecond = 6f;
        [SerializeField] private bool requireRunState = true;

        private float _timer;
        private Vector2 _lastWorld;
        private bool _hasLast;
        private GameManager _gameManager;
        private TrailJuiceSystem _juice;

        private void Awake()
        {
            if (trailField == null)
            {
#if UNITY_2023_1_OR_NEWER
                trailField = FindFirstObjectByType<TrailField>();
#else
                trailField = FindObjectOfType<TrailField>();
#endif
            }

#if UNITY_2023_1_OR_NEWER
            _gameManager = FindFirstObjectByType<GameManager>();
#else
            _gameManager = FindObjectOfType<GameManager>();
#endif
            _juice = TrailJuiceSystem.Instance;
        }

        private void OnEnable()
        {
            _timer = 0f;
            _hasLast = false;
        }

        private void Update()
        {
            if (trailField == null) return;
            if (requireRunState && _gameManager != null && _gameManager.CurrentState != GameState.Run) return;

            var cam = Camera.main;
            if (cam == null) return;

            if (!Input.GetMouseButton(0))
            {
                _hasLast = false;
                return;
            }

            if (_juice != null && _juice.CurrentJuice <= 0f)
            {
                _hasLast = false;
                return;
            }

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = Mathf.Max(0.01f, drawInterval);

            float dt = _timer;
            float cost = juicePerSecond * dt;
            if (_juice != null && !_juice.TryConsume(cost))
            {
                _hasLast = false;
                return;
            }

            Vector2 world = cam.ScreenToWorldPoint(Input.mousePosition);
            if (!_hasLast)
            {
                trailField.Paint(world, drawRadius, drawStrength);
                _lastWorld = world;
                _hasLast = true;
                return;
            }

            float step = Mathf.Max(0.05f, drawRadius * 0.5f);
            float distance = Vector2.Distance(_lastWorld, world);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / step));
            for (int i = 0; i <= steps; i++)
            {
                float t = steps == 0 ? 1f : (float)i / steps;
                Vector2 p = Vector2.Lerp(_lastWorld, world, t);
                trailField.Paint(p, drawRadius, drawStrength);
            }

            _lastWorld = world;
            _hasLast = true;
        }
    }
}

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
        }

        private void Update()
        {
            if (trailField == null) return;
            if (requireRunState && _gameManager != null && _gameManager.CurrentState != GameState.Run) return;

            var cam = Camera.main;
            if (cam == null) return;

            if (!Input.GetMouseButton(0)) return;

            if (_juice != null && _juice.CurrentJuice <= 0f) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = Mathf.Max(0.01f, drawInterval);

            float dt = _timer;
            float cost = juicePerSecond * dt;
            if (_juice != null && !_juice.TryConsume(cost)) return;

            Vector2 world = cam.ScreenToWorldPoint(Input.mousePosition);
            trailField.Paint(world, drawRadius, drawStrength);
        }
    }
}

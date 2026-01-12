using UnityEngine;
using Phosphers.Agents;
namespace Phosphers.Signals
{
    [RequireComponent(typeof(Phospher))]
    public class TrailPainter : MonoBehaviour
    {
        [SerializeField] private TrailField trailField;
        [SerializeField] private float paintRadius = 0.6f;
        [SerializeField] private float paintStrength = 0.25f;
        [SerializeField] private float paintInterval = 0.05f;

        private float _timer;

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
        }

        private void OnEnable()
        {
            _timer = 0f;
        }

        private void Update()
        {
            if (trailField == null) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = Mathf.Max(0.01f, paintInterval);

            trailField.Paint(transform.position, paintRadius, paintStrength);
        }
    }
}

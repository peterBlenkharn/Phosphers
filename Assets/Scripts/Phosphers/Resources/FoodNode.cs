using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Phosphers.Fog;
using Phosphers.Core;

namespace Phosphers.Resources
{
    public class FoodNode : MonoBehaviour
    {
        [Header("Reveal > Activate")]
        [Tooltip("Check this often; 5–10 times/sec is plenty.")]
        [SerializeField] private float revealCheckInterval = 0.15f;
        [Tooltip("If true, re-arm to Inactive at the start of each Run.")]
        [SerializeField] private bool resetOnRunStart = true;

        [Header("Visuals (optional)")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color inactiveColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        [SerializeField] private Color activeColor = new Color(0.95f, 0.85f, 0.25f, 1f);

        [Header("Events")]
        public UnityEvent OnActivated;

        public bool IsActive { get; private set; }

        private Coroutine _pollCo;
        private GameManager _gm;

        private void Reset()
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void OnEnable()
        {
#if UNITY_2023_1_OR_NEWER
            _gm = Object.FindFirstObjectByType<GameManager>();
#else
            _gm = Object.FindObjectOfType<GameManager>();
#endif
            if (_gm != null && resetOnRunStart)
                _gm.OnStateChanged += HandleState;

            ArmInactive();
            _pollCo = StartCoroutine(PollReveal());
        }

        private void OnDisable()
        {
            if (_gm != null && resetOnRunStart)
                _gm.OnStateChanged -= HandleState;

            if (_pollCo != null) StopCoroutine(_pollCo);
            _pollCo = null;
        }

        private void HandleState(GameState s)
        {
            if (s == GameState.Run && resetOnRunStart)
                ArmInactive();
        }

        private void ArmInactive()
        {
            IsActive = false;
            if (spriteRenderer != null) spriteRenderer.color = inactiveColor;
        }

        private IEnumerator PollReveal()
        {
            // light, GC-free polling loop
            var wait = new WaitForSeconds(Mathf.Max(0.05f, revealCheckInterval));
            while (!IsActive)
            {
                var fog = FogSystem.Instance;
                if (fog != null && fog.HasSeen(transform.position))
                    Activate();
                yield return wait;
            }
        }

        private void Activate()
        {
            if (IsActive) return;
            IsActive = true;

            if (spriteRenderer != null) spriteRenderer.color = activeColor;

            // notify listeners (ResourceSystem, VFX, etc.)
            try { OnActivated?.Invoke(); } catch (System.Exception e) { Debug.LogException(e, this); }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = IsActive ? new Color(1f, 0.9f, 0.2f, 0.8f) : new Color(0.5f, 0.5f, 0.5f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);
        }
    }
}

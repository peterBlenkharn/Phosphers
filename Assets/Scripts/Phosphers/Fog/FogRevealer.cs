using UnityEngine;
using Phosphers.Fog;
using Phosphers.Agents;

namespace Phosphers.Fog
{
    public class FogRevealer : MonoBehaviour
    {
        [Header("Target & Radius")]
        [SerializeField] private Transform target;
        [SerializeField] private float radius = 1.2f;

        [Header("Ticking")]
        [SerializeField] private float interval = 0.06f;
        [SerializeField] private bool randomizeFirstTick = true;

        [Header("Per-state scaling (if a Phospher is present)")]
        [SerializeField] private bool scaleByPhospherState = true;
        [SerializeField] private StateRadiusMultipliers stateMultipliers = new StateRadiusMultipliers(1f, 1f, 1f);

        [Header("Fade-in (intensity ramp 0 > 1)")]
        [SerializeField] private bool fadeInEnabled = true;
        [SerializeField] private StateFadeSeconds fadeInSeconds = new StateFadeSeconds(0.20f, 0.20f, 0.20f);
        [SerializeField] private Easing easing = Easing.OutQuad;

        [Header("Sampling Offset")]
        [SerializeField] private float forwardSampleOffset = 0f;
        [SerializeField] private bool useVelocityDirection = true;

        [Header("Enable/Disable")]
        [SerializeField] private bool revealEnabled = true;

        private float _timer;
        private Phospher _phospher;             // optional
        private PhospherState _lastState;
        private float _fadeT;                    // 0..1 (pre-ease)
        private float _currFadeDuration;         // seconds for current state

        public void SetEnabled(bool v) => revealEnabled = v;

        private void Awake()
        {
            if (target == null) target = transform;
            _phospher = GetComponent<Phospher>();
            _lastState = _phospher != null ? _phospher.State : PhospherState.Forage;
            _currFadeDuration = GetFadeSeconds(_lastState);
            _fadeT = fadeInEnabled ? 0f : 1f;
        }

        private void OnEnable()
        {
            _timer = randomizeFirstTick ? Random.Range(0f, Mathf.Max(0.01f, interval)) : interval;
        }

        private void Update()
        {
            if (!revealEnabled) return;

            // Handle state change to reset fade
            var stateNow = _phospher != null ? _phospher.State : PhospherState.Forage;
            if (stateNow != _lastState)
            {
                _lastState = stateNow;
                _currFadeDuration = GetFadeSeconds(stateNow);
                _fadeT = fadeInEnabled ? 0f : 1f;
            }

            // Advance fade (0to1) with chosen duration independent of reveal tick
            if (fadeInEnabled && _fadeT < 1f && _currFadeDuration > 0.001f)
            {
                _fadeT = Mathf.Clamp01(_fadeT + Time.deltaTime / _currFadeDuration);
            }
            float intensity = fadeInEnabled ? ApplyEasing(_fadeT, easing) : 1f;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer += Mathf.Max(0.01f, interval);

            var fog = FogSystem.Instance;
            if (fog == null) return;

            // Sample position (optional forward offset)
            Vector2 pos = target.position;
            if (forwardSampleOffset > 0f)
            {
                Vector2 fwd;
                if (useVelocityDirection && _phospher != null && _phospher.Velocity.sqrMagnitude > 1e-8f)
                    fwd = _phospher.Velocity.normalized;
                else
                    fwd = target.right;
                pos += fwd * forwardSampleOffset;
            }

            // Final radius with per-state multiplier
            float r = radius;
            if (scaleByPhospherState && _phospher != null)
            {
                switch (stateNow)
                {
                    case PhospherState.Forage: r *= Mathf.Max(0f, stateMultipliers.forage); break;
                    case PhospherState.Return: r *= Mathf.Max(0f, stateMultipliers.returnMul); break;
                    case PhospherState.Recover: r *= Mathf.Max(0f, stateMultipliers.recover); break;
                }
            }

            if (r > 0f && intensity > 0f)
                fog.Reveal(pos, r, intensity);
        }

        private float GetFadeSeconds(PhospherState s)
        {
            switch (s)
            {
                case PhospherState.Forage: return Mathf.Max(0f, fadeInSeconds.forage);
                case PhospherState.Return: return Mathf.Max(0f, fadeInSeconds.returnMul);
                case PhospherState.Recover: return Mathf.Max(0f, fadeInSeconds.recover);
                default: return 0.2f;
            }
        }

        private static float ApplyEasing(float t, Easing e)
        {
            t = Mathf.Clamp01(t);
            switch (e)
            {
                case Easing.Linear: return t;
                case Easing.SmoothStep: return t * t * (3f - 2f * t);
                case Easing.OutQuad: return 1f - (1f - t) * (1f - t);
                case Easing.OutCubic: { float u = 1f - t; return 1f - u * u * u; }
                default: return t;
            }
        }

        [System.Serializable]
        public struct StateRadiusMultipliers
        {
            public float forage, returnMul, recover;
            public StateRadiusMultipliers(float f, float r, float c) { forage = f; returnMul = r; recover = c; }
        }

        [System.Serializable]
        public struct StateFadeSeconds
        {
            public float forage, returnMul, recover;
            public StateFadeSeconds(float f, float r, float c) { forage = f; returnMul = r; recover = c; }
        }

        public enum Easing { Linear, SmoothStep, OutQuad, OutCubic }
    }
}

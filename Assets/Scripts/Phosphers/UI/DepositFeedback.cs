using UnityEngine;
using Phosphers.Core;
using Phosphers.World;
using Phosphers.Resources;

namespace Phosphers.UI
{
    public class DepositFeedback : MonoBehaviour
    {
        [Header("Anchor")]
        [SerializeField] private Anchor anchor;            // drag Anchor (for position/outer radius)
        [SerializeField] private Transform pulseTarget;    // e.g., anchor sprite transform
        [SerializeField] private SpriteRenderer pulseSprite; // optional tint target

        [Header("Pulse")]
        [SerializeField] private float pulseScale = 1.12f; // scale peak
        [SerializeField] private float pulseTime = 0.12f; // seconds
        [SerializeField] private Color pulseTint = new Color(1f, 0.95f, 0.6f, 1f);

        [Header("Optional Particles")]
        [SerializeField] private ParticleSystem depositParticles; // optional; plays at rim
        [SerializeField] private float particleRadiusFactor = 1.02f; // slightly outside rim

        private Vector3 _baseScale;
        private Color _baseColor;
        private bool _pulsing;

        private void Awake()
        {
            if (!anchor) anchor = FindObjectOfType<Anchor>();
            if (!pulseTarget && anchor) pulseTarget = anchor.transform;
            if (pulseSprite) _baseColor = pulseSprite.color;
            _baseScale = pulseTarget ? pulseTarget.localScale : Vector3.one;
        }

        private void OnEnable()
        {
            var rs = ResourceSystem.Instance;
            if (rs != null) rs.OnBitDeposited += HandleDeposit;
        }

        private void OnDisable()
        {
            var rs = ResourceSystem.Instance;
            if (rs != null) rs.OnBitDeposited -= HandleDeposit;
        }

        private void HandleDeposit(Phosphers.Agents.Phospher p, BitSpec spec)
        {
            if (pulseTarget) StartCoroutine(PulseRoutine());

            if (depositParticles && anchor)
            {
                // pick a random point on the rim for a subtle burst
                float r = anchor.OuterRadius * particleRadiusFactor;
                float a = Random.Range(0f, Mathf.PI * 2f);
                Vector3 pos = (Vector2)anchor.transform.position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                var main = depositParticles.main;
                depositParticles.transform.position = pos;
                depositParticles.Play();
            }
        }

        private System.Collections.IEnumerator PulseRoutine()
        {
            if (_pulsing) yield break; // prevent overlap; simple and cheap
            _pulsing = true;

            float t = 0f;
            var startScale = _baseScale;
            var endScale = startScale * pulseScale;
            var startCol = pulseSprite ? _baseColor : Color.white;

            // ease-out
            while (t < pulseTime)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / pulseTime);
                float e = 1f - (1f - u) * (1f - u); // quad ease-out

                if (pulseTarget) pulseTarget.localScale = Vector3.LerpUnclamped(startScale, endScale, e);
                if (pulseSprite) pulseSprite.color = Color.LerpUnclamped(startCol, pulseTint, e);
                yield return null;
            }

            // snap back
            if (pulseTarget) pulseTarget.localScale = _baseScale;
            if (pulseSprite) pulseSprite.color = _baseColor;
            _pulsing = false;
        }
    }
}

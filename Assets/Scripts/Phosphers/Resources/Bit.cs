using UnityEngine;

namespace Phosphers.Resources
{
    [RequireComponent(typeof(Collider2D))]
    public class Bit : MonoBehaviour, IBitTarget
    {
        [Header("Spec (default if not overridden by spawner)")]
        [SerializeField] private BitType type = BitType.Generic;
        [SerializeField] private int value = 1;
        [SerializeField] private float weight = 1f;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer sr;

        private Collider2D _col;

        // Link back to source that emitted this (for concurrent caps)
        public FoodSource Owner { get; set; }   // NEW

        // IBitTarget API
        public bool IsAvailable { get; private set; } = true;
        public Transform Transform => transform;
        public Vector2 Position => transform.position;
        public BitSpec Spec => new BitSpec(type, value, weight);

        private void Reset()
        {
            sr = GetComponentInChildren<SpriteRenderer>();
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
            gameObject.layer = LayerMask.NameToLayer("Bits");
        }

        private void Awake()
        {
            _col = GetComponent<Collider2D>();
            if (_col != null) _col.isTrigger = true;
        }

        // Let spawners decide rotation explicitly (no auto-random on enable)
        public void ApplyRandomRotation(System.Random rng, Vector2 rangeDeg)
        {
            if (sr == null) return;
            float t = (float)rng.NextDouble();
            float z = Mathf.Lerp(rangeDeg.x, rangeDeg.y, t);
            sr.transform.localRotation = Quaternion.Euler(0, 0, z);
        }

        public void Configure(BitSpec spec)
        {
            type = spec.type; value = spec.value; weight = spec.weight;
        }

        public void MarkAvailable(bool available)
        {
            IsAvailable = available;
            if (_col != null) _col.enabled = available;
            if (sr != null) sr.enabled = available;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var p = other.GetComponentInParent<Phosphers.Agents.Phospher>();
            if (p == null) return;
            if (!p.TryPickup(Spec)) return;

            Phosphers.Core.ResourceSystem.Instance?.DespawnBit(this);
        }
    }
}

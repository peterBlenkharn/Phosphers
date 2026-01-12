using System.Collections.Generic;
using UnityEngine;
using Phosphers.Resources;

namespace Phosphers.Agents
{
    [System.Serializable]
    public class PhospherInventory
    {
        [Header("Capacity")]
        public float maxWeight = 1f;           // MVP default: 1 Generic bit
        public int maxCountPerType = 1;

        [Header("Runtime (read-only)")]
        [SerializeField] private float currentWeight;
        private readonly Dictionary<BitType, int> counts = new();

        private readonly List<BitSpec> _items = new();

        public float CurrentWeight => currentWeight;
        public bool IsEmpty => _items.Count == 0;                 // ? more robust than weight check

        // --- Query ---
        public bool CanAccept(BitSpec spec)
        {
            if (currentWeight + spec.weight > maxWeight) return false;
            int c = counts.TryGetValue(spec.type, out var n) ? n : 0;
            return c < maxCountPerType;
        }
        public bool IsFullFor(BitSpec spec) => !CanAccept(spec);

        // --- Add / Remove ---
        /// <summary>Adds without checking. Prefer TryAdd in gameplay code.</summary>
        public void Add(BitSpec spec)
        {
            currentWeight += spec.weight;
            _items.Add(spec);
            if (counts.ContainsKey(spec.type)) counts[spec.type] += 1;
            else counts[spec.type] = 1;
        }

        /// <summary>Safe add that respects capacity limits.</summary>
        public bool TryAdd(BitSpec spec)
        {
            if (!CanAccept(spec)) return false;
            Add(spec);
            return true;
        }

        /// <summary>Removes one instance matching the given spec's type (last-in of that type).</summary>
        public void Remove(BitSpec spec)
        {
            // remove last item of this type to keep things simple/predictable
            int idx = _items.FindLastIndex(b => b.type == spec.type);
            if (idx >= 0)
            {
                var removed = _items[idx];
                _items.RemoveAt(idx);
                currentWeight = Mathf.Max(0f, currentWeight - removed.weight);

                if (counts.TryGetValue(removed.type, out var n))
                {
                    n = Mathf.Max(0, n - 1);
                    if (n == 0) counts.Remove(removed.type);
                    else counts[removed.type] = n;
                }
            }
        }

        /// <summary>Clears everything (weight, counts, and items).</summary>
        public void Clear()
        {
            currentWeight = 0f;
            counts.Clear();
            _items.Clear();                                  // ? was missing
        }

        // --- Pop / Take ---
        /// <summary>
        /// Takes exactly one carried item (LIFO). Returns false if empty.
        /// </summary>
        public bool TryTakeOne(out BitSpec spec)
        {
            if (_items.Count == 0)
            {
                spec = default;                              // ? when empty
                return false;
            }

            // Pop last carried item
            int last = _items.Count - 1;
            spec = _items[last];                             // ? this is the spec you were asking for
            _items.RemoveAt(last);

            // Update weight and counts
            currentWeight = Mathf.Max(0f, currentWeight - spec.weight);
            if (counts.TryGetValue(spec.type, out var n))
            {
                n = Mathf.Max(0, n - 1);
                if (n == 0) counts.Remove(spec.type);
                else counts[spec.type] = n;
            }

            return true;
        }

        /// <summary>
        /// Same as TryTakeOne but returns true if anything was popped; alias kept for your existing calls.
        /// </summary>
        public bool TryPopAny(out BitSpec spec)
        {
            return TryTakeOne(out spec);
        }
    }
}

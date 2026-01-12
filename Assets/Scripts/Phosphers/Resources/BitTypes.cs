namespace Phosphers.Resources
{
    public enum BitType { Generic /*, Rare, Heavy, ...*/ }

    // Value/weight per type (we’ll likely move to a ScriptableObject table later)
    public struct BitSpec
    {
        public BitType type;
        public int value;
        public float weight;
        public BitSpec(BitType t, int v, float w) { type = t; value = v; weight = w; }

        public static readonly BitSpec Generic = new BitSpec(BitType.Generic, 1, 1f);
    }

    // Minimal interface any “Bit” must implement for perception/pickup.
    public interface IBitTarget
    {
        UnityEngine.Transform Transform { get; }
        UnityEngine.Vector2 Position { get; }
        BitSpec Spec { get; }
        bool IsAvailable { get; } // true iff in the world and collectable
    }
}

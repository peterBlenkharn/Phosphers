using UnityEngine;

namespace Phosphers.Signals
{
    public interface IVectorField
    {
        /// Returns a world-space vector (can be zero) at the given position.
        Vector2 Sample(Vector2 worldPos);
    }
}

using UnityEngine;

namespace Phosphers.Signals
{
    public class NullSignalField : MonoBehaviour, IVectorField
    {
        public Vector2 Sample(Vector2 worldPos) => Vector2.zero;
    }
}

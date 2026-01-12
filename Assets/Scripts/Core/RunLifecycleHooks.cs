using UnityEngine;
using Phosphers.Core;

namespace Phosphers.Core
{
    /// Subscribes to GameManager state changes and coordinates ResourceSystem on Run start/end.
    public class RunLifecycleHooks : MonoBehaviour
    {
        private GameManager _gm;
        private ResourceSystem _rs;

        private void OnEnable()
        {
#if UNITY_2023_1_OR_NEWER
            _gm = FindFirstObjectByType<GameManager>();
#else
            _gm = FindObjectOfType<GameManager>();
#endif
            _rs = ResourceSystem.Instance;

            if (_gm != null) _gm.OnStateChanged += HandleState;
        }

        private void OnDisable()
        {
            if (_gm != null) _gm.OnStateChanged -= HandleState;
        }

        private void HandleState(GameState s)
        {
            if (_rs == null) _rs = ResourceSystem.Instance;
            if (_rs == null) return;

            switch (s)
            {
                case GameState.Run:
                    _rs.DespawnAllActive();  // start with a clean field
                    _rs.ResetScore();        // score = 0 at the beginning of each Run
                    break;

                case GameState.End:
                case GameState.Menu:
                    _rs.DespawnAllActive();  // clear leftover bits to avoid clutter
                    break;
            }
        }
    }
}

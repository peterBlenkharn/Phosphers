using System;
using UnityEngine;

namespace Phosphers.Core
{
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        [Header("Run Objects")]
        [SerializeField] private GameObject anchorPrefab;
        [SerializeField] private Vector3 anchorSpawnPosition = Vector3.zero;

        [Header("Debug")]
        [SerializeField] private bool enableDevHotkeys = true;

        public GameState CurrentState { get; private set; } = GameState.Menu;
        public event Action<GameState> OnStateChanged;

        private GameObject _spawnedAnchor;
        public Transform AnchorTransform => _spawnedAnchor != null ? _spawnedAnchor.transform : null;


        private void Awake()
        {
            SetState(GameState.Menu);
        }

        private void Update()
        {
            if (!enableDevHotkeys) return;

            if (CurrentState == GameState.Menu && Input.GetKeyDown(KeyCode.Space))
                StartRun();

            if (CurrentState == GameState.Run && Input.GetKeyDown(KeyCode.E))
                EndRun();

            if (CurrentState == GameState.End && Input.GetKeyDown(KeyCode.R))
                ReturnToMenu();
        }

        public void StartRun()
        {
            if (CurrentState != GameState.Menu) return;

            // Spawn per run objects
            if (anchorPrefab != null && _spawnedAnchor == null)
                _spawnedAnchor = Instantiate(anchorPrefab, anchorSpawnPosition, Quaternion.identity);

            SetState(GameState.Run);
        }

        public void EndRun()
        {
            if (CurrentState != GameState.Run) return;

            // Clean up per run objects
            if (_spawnedAnchor != null)
            {
                Destroy(_spawnedAnchor);
                _spawnedAnchor = null;
            }

            SetState(GameState.End);
        }

        public void ReturnToMenu()
        {
            if (CurrentState != GameState.End) return;
            SetState(GameState.Menu);
        }

        private void SetState(GameState newState)
        {
            CurrentState = newState;
            Debug.Log($"[GameManager] State to {newState}");
            OnStateChanged?.Invoke(newState);
        }
    }
}

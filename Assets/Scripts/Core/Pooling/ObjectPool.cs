using System.Collections.Generic;
using UnityEngine;

namespace Phosphers.Core.Pooling
{
    public class ObjectPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Queue<T> _pool = new();
        private readonly HashSet<T> _active = new();
        private readonly Transform _activeParent;
        private readonly Transform _inactiveParent;
        private readonly bool _logWarnings;

        public IReadOnlyCollection<T> Active => _active;

        public ObjectPool(T prefab, int initialSize, Transform activeParent, Transform inactiveParent, bool logWarnings)
        {
            _prefab = prefab;
            _activeParent = activeParent;
            _inactiveParent = inactiveParent;
            _logWarnings = logWarnings;

            if (_prefab == null)
            {
                Debug.LogError("[ObjectPool] Prefab is null.");
                return;
            }

            Warm(initialSize);
        }

        public void Warm(int count)
        {
            if (_prefab == null || count <= 0) return;
            for (int i = 0; i < count; i++)
            {
                var item = Object.Instantiate(_prefab, _inactiveParent);
                if (item is IPoolable poolable) poolable.OnDespawned();
                item.gameObject.SetActive(false);
                _pool.Enqueue(item);
            }
        }

        public T Get(Vector3 position, Quaternion rotation)
        {
            if (_pool.Count == 0)
            {
                if (_logWarnings) Debug.LogWarning("[ObjectPool] Pool exhausted.");
                return null;
            }

            var item = _pool.Dequeue();
            _active.Add(item);

            var t = item.transform;
            if (_activeParent != null) t.SetParent(_activeParent, false);
            t.SetPositionAndRotation(position, rotation);

            item.gameObject.SetActive(true);
            if (item is IPoolable poolable) poolable.OnSpawned();
            return item;
        }

        public bool Release(T item)
        {
            if (item == null) return false;
            if (!_active.Remove(item)) return false;

            if (item is IPoolable poolable) poolable.OnDespawned();
            item.gameObject.SetActive(false);

            if (_inactiveParent != null) item.transform.SetParent(_inactiveParent, false);
            _pool.Enqueue(item);
            return true;
        }

        public void ReleaseAll()
        {
            if (_active.Count == 0) return;
            var copy = new T[_active.Count];
            _active.CopyTo(copy);
            foreach (var item in copy)
            {
                Release(item);
            }
        }
    }
}

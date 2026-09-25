using System.Collections.Generic;
using UnityEngine;

namespace ShadowVale.Core.Pooling
{
    /// <summary>
    /// Simple prefab pool for projectiles, VFX and corpses. Never instantiate bullets per shot;
    /// the 60 FPS target depends on it.
    /// </summary>
    public sealed class ObjectPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Stack<T> _free = new();

        public int CountFree => _free.Count;
        public int CountTotal { get; private set; }

        public ObjectPool(T prefab, int prewarm = 0, Transform parent = null)
        {
            _prefab = prefab;
            _parent = parent;
            for (var i = 0; i < prewarm; i++) _free.Push(Create());
        }

        public T Get(Vector3 position, Quaternion rotation)
        {
            var item = _free.Count > 0 ? _free.Pop() : Create();
            var t = item.transform;
            t.SetPositionAndRotation(position, rotation);
            item.gameObject.SetActive(true);
            return item;
        }

        public void Release(T item)
        {
            item.gameObject.SetActive(false);
            _free.Push(item);
        }

        private T Create()
        {
            var item = Object.Instantiate(_prefab, _parent);
            item.gameObject.SetActive(false);
            CountTotal++;
            return item;
        }
    }
}

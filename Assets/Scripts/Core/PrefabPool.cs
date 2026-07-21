using UnityEngine;
using UnityEngine.Pool;

namespace TankDuel.Core
{
    /// <summary>
    /// Пул экземпляров одного префаба поверх встроенного UnityEngine.Pool.
    /// Зачем: в Web-билде Instantiate/Destroy в каждом кадре — это мусор и просадки,
    /// а фаза фарма как раз спавнит и убивает десятки объектов за минуту.
    /// </summary>
    public class PrefabPool
    {
        readonly GameObject prefab;
        readonly Transform parent;
        readonly ObjectPool<GameObject> pool;

        public PrefabPool(GameObject prefab, Transform parent, int prewarm = 8)
        {
            this.prefab = prefab;
            this.parent = parent;

            pool = new ObjectPool<GameObject>(
                createFunc: Create,
                actionOnGet: OnGet,
                actionOnRelease: OnRelease,
                actionOnDestroy: OnDestroyInstance,
                collectionCheck: false, // отключено: Release идемпотентен через PooledObject
                defaultCapacity: Mathf.Max(1, prewarm));

            Prewarm(prewarm);
        }

        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            var go = pool.Get();
            go.transform.SetPositionAndRotation(position, rotation);
            return go;
        }

        public void Release(GameObject instance)
        {
            if (instance != null)
                pool.Release(instance);
        }

        void Prewarm(int count)
        {
            if (count <= 0)
                return;

            var buffer = new GameObject[count];
            for (int i = 0; i < count; i++)
                buffer[i] = pool.Get();
            for (int i = 0; i < count; i++)
                pool.Release(buffer[i]);
        }

        GameObject Create()
        {
            var go = Object.Instantiate(prefab, parent);
            go.name = prefab.name;

            var pooled = go.GetComponent<PooledObject>();
            if (pooled == null)
                pooled = go.AddComponent<PooledObject>();
            pooled.BindTo(this);

            return go;
        }

        void OnGet(GameObject go)
        {
            go.GetComponent<PooledObject>().MarkTaken();
            go.SetActive(true);
        }

        void OnRelease(GameObject go)
        {
            go.GetComponent<PooledObject>().MarkReleased();
            go.SetActive(false);
        }

        void OnDestroyInstance(GameObject go)
        {
            if (go != null)
                Object.Destroy(go);
        }
    }
}

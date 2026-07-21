using System.Collections.Generic;
using UnityEngine;

namespace TankDuel.Core
{
    /// <summary>
    /// Реестр «префаб → пул». Создаётся сам при первом обращении,
    /// в сцену руками класть не нужно (единственное исключение из решения №8:
    /// это не игровой объект, а служебный контейнер).
    /// </summary>
    public class PoolManager : MonoBehaviour
    {
        static PoolManager instance;

        readonly Dictionary<GameObject, PrefabPool> pools = new Dictionary<GameObject, PrefabPool>();

        // Play Mode без Domain Reload не обнуляет статику сам — делаем руками
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        static PoolManager Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("~PoolManager");
                    instance = go.AddComponent<PoolManager>();
                }
                return instance;
            }
        }

        public static GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return Instance.GetPool(prefab).Get(position, rotation);
        }

        public static void Release(GameObject instance) => PooledObject.Despawn(instance);

        PrefabPool GetPool(GameObject prefab)
        {
            if (!pools.TryGetValue(prefab, out var pool))
            {
                // Контейнер на каждый префаб — чтобы иерархия в Play Mode оставалась читаемой
                var container = new GameObject($"Pool [{prefab.name}]");
                container.transform.SetParent(transform, false);

                pool = new PrefabPool(prefab, container.transform);
                pools[prefab] = pool;
            }
            return pool;
        }

        void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
    }
}

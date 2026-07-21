using UnityEngine;

namespace TankDuel.Core
{
    /// <summary>
    /// Метка на экземпляре из пула: помнит, в какой пул возвращаться.
    /// Вешается автоматически при создании экземпляра, руками ставить не нужно.
    /// </summary>
    public class PooledObject : MonoBehaviour
    {
        PrefabPool pool;
        bool inPool;

        public void BindTo(PrefabPool owner) => pool = owner;

        /// <summary>Пул забрал объект обратно (вызывает сам пул).</summary>
        internal void MarkReleased() => inPool = true;

        /// <summary>Пул выдал объект наружу (вызывает сам пул).</summary>
        internal void MarkTaken() => inPool = false;

        /// <summary>Вернуть в пул. Повторный вызов безопасен — например, если снаряд задел двоих в один кадр.</summary>
        public void Release()
        {
            if (inPool || pool == null)
                return;
            pool.Release(gameObject);
        }

        /// <summary>Убрать объект правильным способом: из пула — в пул, обычный — Destroy.</summary>
        public static void Despawn(GameObject go)
        {
            if (go == null)
                return;

            var pooled = go.GetComponent<PooledObject>();
            if (pooled != null)
                pooled.Release();
            else
                Object.Destroy(go);
        }
    }
}

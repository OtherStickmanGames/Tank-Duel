using TankDuel.Core;
using UnityEngine;

namespace TankDuel.Tank
{
    /// <summary>
    /// Снаряд: летит по прямой, бьёт первый чужой Health на пути, об стены умирает.
    /// Идёт через пул (задача 2.1), если назначен префаб; без префаба —
    /// запасной путь на примитивах, без пула.
    /// У префаба коллайдер должен быть isTrigger.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        float speed;
        float damage;
        float deathTime;
        int team;

        public static void Spawn(Projectile prefab, Vector3 position, Vector3 direction,
            float speed, float lifetime, float damage, int team)
        {
            Projectile p;
            if (prefab != null)
            {
                var go = PoolManager.Get(prefab.gameObject, position, Quaternion.LookRotation(direction));
                p = go.GetComponent<Projectile>();
            }
            else
            {
                p = CreateFromPrimitive(position, direction);
            }

            p.speed = speed;
            p.damage = damage;
            p.team = team;
            p.deathTime = Time.time + lifetime;
        }

        static Projectile CreateFromPrimitive(Vector3 position, Vector3 direction)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile";
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            go.transform.localScale = Vector3.one * 0.35f;
            go.GetComponent<Collider>().isTrigger = true;

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            return go.AddComponent<Projectile>();
        }

        void Update()
        {
            transform.position += transform.forward * (speed * Time.deltaTime);
            if (Time.time >= deathTime)
                PooledObject.Despawn(gameObject);
        }

        void OnTriggerEnter(Collider other)
        {
            var health = other.GetComponentInParent<Health>();
            if (health != null)
            {
                if (health.team == team)
                    return; // своих не бьём, включая стрелявшего

                health.TakeDamage(damage);
                PooledObject.Despawn(gameObject);
            }
            else if (!other.isTrigger)
            {
                PooledObject.Despawn(gameObject); // стена, декорации
            }
        }
    }
}

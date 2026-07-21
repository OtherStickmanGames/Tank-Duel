using UnityEngine;

namespace TankDuel.Core
{
    /// <summary>
    /// Простая смерть для мишеней и разрушаемых объектов: умер — исчез.
    /// Объект из пула вернётся в пул, обычный — уничтожится.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class DestroyOnDeath : MonoBehaviour
    {
        void Awake()
        {
            GetComponent<Health>().Died += _ => PooledObject.Despawn(gameObject);
        }
    }
}

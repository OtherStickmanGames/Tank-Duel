using UnityEngine;

namespace TankDuel.Core
{
    /// <summary>Простая смерть для мишеней и разрушаемых объектов: умер — исчез.</summary>
    [RequireComponent(typeof(Health))]
    public class DestroyOnDeath : MonoBehaviour
    {
        void Awake()
        {
            GetComponent<Health>().Died += _ => Destroy(gameObject);
        }
    }
}

using UnityEngine;

namespace TankDuel.Data
{
    /// <summary>
    /// Базовые характеристики танка (уровень 0, без прокачки).
    /// Все балансные числа живут здесь и в UpgradeConfig — в коде чисел нет.
    /// </summary>
    [CreateAssetMenu(fileName = "TankConfig", menuName = "Tank Duel/Tank Config")]
    public class TankConfig : ScriptableObject
    {
        [Header("База (без прокачки)")]
        public float baseHealth = 100f;
        public float baseDamage = 10f;
        [Tooltip("Выстрелов в секунду")]
        public float baseFireRate = 1f;
        [Tooltip("Метров в секунду")]
        public float baseMoveSpeed = 5f;

        [Header("Снаряд")]
        public float projectileSpeed = 20f;
        [Tooltip("Секунд жизни снаряда, если ни во что не попал")]
        public float projectileLifetime = 3f;
    }
}

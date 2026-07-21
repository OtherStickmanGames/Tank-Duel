using System;

namespace TankDuel.Data
{
    /// <summary>
    /// Итоговые характеристики танка, посчитанные из TankBuild + конфигов.
    /// Значение-результат: не хранить, а пересчитывать при каждом изменении билда.
    /// </summary>
    [Serializable]
    public struct TankStats
    {
        public float maxHealth;
        public float damage;
        public float fireRate;  // выстрелов в секунду
        public float moveSpeed; // метров в секунду
    }
}

using System;
using UnityEngine;

namespace TankDuel.Core
{
    /// <summary>
    /// Общее здоровье для всего, что можно убить: танки, фарм-боты, разрушаемые объекты.
    /// team: 0 — сторона игрока, 1 — сторона оппонента. Свои снаряды своих не бьют.
    /// </summary>
    public class Health : MonoBehaviour
    {
        public float maxHealth = 100f;
        public int team;

        public float Current { get; private set; }
        public float Max => maxHealth;
        public bool IsDead => Current <= 0f;

        public event Action<Health> Died;
        public event Action<float, float> Changed; // (current, max)

        void Awake()
        {
            Current = maxHealth;
        }

        /// <summary>Новый максимум (прокачка HP). Прибавка к максимуму добавляется и к текущему.</summary>
        public void SetMax(float newMax)
        {
            float delta = newMax - maxHealth;
            maxHealth = newMax;
            if (delta > 0f)
                Current += delta;
            Current = Mathf.Min(Current, maxHealth);
            Changed?.Invoke(Current, maxHealth);
        }

        public void TakeDamage(float amount)
        {
            if (IsDead)
                return;

            Current = Mathf.Max(0f, Current - amount);
            Changed?.Invoke(Current, maxHealth);

            if (Current <= 0f)
                Died?.Invoke(this);
        }

        public void ResetFull()
        {
            Current = maxHealth;
            Changed?.Invoke(Current, maxHealth);
        }
    }
}

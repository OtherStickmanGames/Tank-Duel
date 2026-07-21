using System;
using UnityEngine;

namespace TankDuel.Data
{
    /// <summary>
    /// Что, сколько и когда спавнится на половине игрока во время фазы фарма.
    /// Весь тюнинг фарма живёт здесь — в коде спавнера чисел нет.
    /// </summary>
    [CreateAssetMenu(fileName = "WaveConfig", menuName = "Tank Duel/Wave Config")]
    public class WaveConfig : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string label = "Волна";
            public GameObject prefab;

            [Tooltip("С какой секунды фарма начинает спавниться")]
            public float startDelay;
            [Tooltip("Пауза между спавнами, сек")]
            public float interval = 2f;
            [Tooltip("Потолок одновременно живых от этой волны")]
            public int maxAlive = 5;
            [Tooltip("Сколько всего заспавнить за фазу. 0 — без ограничения")]
            public int totalLimit;
            [Tooltip("Здоровье экземпляра")]
            public float health = 30f;
            [Tooltip("Высота центра объекта над землёй")]
            public float spawnHeight = 1f;
            [Tooltip("Минимальный отступ от танков и других заспавненных объектов")]
            public float minSpacing = 2.5f;
        }

        public Entry[] entries;
    }
}

using System;
using UnityEngine;

namespace TankDuel.Data
{
    /// <summary>
    /// Цены и приросты прокачки по уровням.
    /// steps[i] трека — переход с уровня i на уровень i+1.
    /// Количество шагов в треке = максимальный уровень оси.
    /// </summary>
    [CreateAssetMenu(fileName = "UpgradeConfig", menuName = "Tank Duel/Upgrade Config")]
    public class UpgradeConfig : ScriptableObject
    {
        [Serializable]
        public struct Step
        {
            [Tooltip("Цена перехода на следующий уровень, в очках")]
            public int cost;
            [Tooltip("Плоская прибавка к стату за этот уровень")]
            public float bonus;
        }

        [Serializable]
        public class Track
        {
            public UpgradeType type;
            public Step[] steps;
        }

        public Track[] tracks;

        public int GetMaxLevel(UpgradeType type)
        {
            var track = FindTrack(type);
            return track?.steps?.Length ?? 0;
        }

        /// <summary>Цена перехода с уровня currentLevel на следующий. -1, если уровень максимальный.</summary>
        public int GetCost(UpgradeType type, int currentLevel)
        {
            var track = FindTrack(type);
            if (track?.steps == null || currentLevel < 0 || currentLevel >= track.steps.Length)
                return -1;
            return track.steps[currentLevel].cost;
        }

        /// <summary>Суммарная прибавка к стату на уровне level (сумма шагов 0..level-1).</summary>
        public float GetTotalBonus(UpgradeType type, int level)
        {
            var track = FindTrack(type);
            if (track?.steps == null)
                return 0f;

            float total = 0f;
            int count = Mathf.Min(level, track.steps.Length);
            for (int i = 0; i < count; i++)
                total += track.steps[i].bonus;
            return total;
        }

        Track FindTrack(UpgradeType type)
        {
            if (tracks == null)
                return null;

            foreach (var track in tracks)
            {
                if (track != null && track.type == type)
                    return track;
            }
            return null;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace TankDuel.Data
{
    /// <summary>
    /// Билд танка — чистые данные, без ссылок на сцену и компоненты.
    /// Сериализуется в JSON: профиль игрока, слепок билда для бота, передача по сети.
    /// Правила «что сколько стоит и что даёт» живут в TankConfig/UpgradeConfig,
    /// билд хранит только выбор игрока.
    /// </summary>
    [Serializable]
    public class TankBuild
    {
        public string hullId = "default";
        public string gunId = "default";

        [SerializeField] int[] upgradeLevels = new int[UpgradeTypesTotal];
        public List<PerkType> perks = new List<PerkType>();

        // Держать в синхроне с enum UpgradeType.
        public const int UpgradeTypesTotal = 4;

        public int GetLevel(UpgradeType type)
        {
            EnsureSize();
            return upgradeLevels[(int)type];
        }

        /// <summary>Есть ли ещё уровни в этой оси. Хватает ли очков — проверяет вызывающий (ScoreSystem).</summary>
        public bool CanUpgrade(UpgradeType type, UpgradeConfig config)
        {
            return GetLevel(type) < config.GetMaxLevel(type);
        }

        /// <summary>Поднять уровень оси на 1. false — уровень уже максимальный.</summary>
        public bool Upgrade(UpgradeType type, UpgradeConfig config)
        {
            if (!CanUpgrade(type, config))
                return false;

            upgradeLevels[(int)type]++;
            return true;
        }

        /// <summary>
        /// Итоговые статы: база из TankConfig + сумма прибавок по уровням из UpgradeConfig.
        /// Эффекты перков будут добавляться здесь же (этап 5) —
        /// геймплейный код всегда видит только готовые TankStats.
        /// </summary>
        public TankStats ComputeStats(TankConfig tank, UpgradeConfig upgrades)
        {
            return new TankStats
            {
                maxHealth = tank.baseHealth + upgrades.GetTotalBonus(UpgradeType.Health, GetLevel(UpgradeType.Health)),
                damage = tank.baseDamage + upgrades.GetTotalBonus(UpgradeType.Damage, GetLevel(UpgradeType.Damage)),
                fireRate = tank.baseFireRate + upgrades.GetTotalBonus(UpgradeType.FireRate, GetLevel(UpgradeType.FireRate)),
                moveSpeed = tank.baseMoveSpeed + upgrades.GetTotalBonus(UpgradeType.MoveSpeed, GetLevel(UpgradeType.MoveSpeed)),
            };
        }

        public string ToJson() => JsonUtility.ToJson(this);

        public static TankBuild FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new TankBuild();

            var build = JsonUtility.FromJson<TankBuild>(json) ?? new TankBuild();
            build.EnsureSize();
            build.perks ??= new List<PerkType>();
            return build;
        }

        /// <summary>Защита от старых сейвов: если осей прокачки стало больше, массив дорастает сам.</summary>
        void EnsureSize()
        {
            if (upgradeLevels == null)
                upgradeLevels = new int[UpgradeTypesTotal];
            else if (upgradeLevels.Length < UpgradeTypesTotal)
                Array.Resize(ref upgradeLevels, UpgradeTypesTotal);
        }
    }
}

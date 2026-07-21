using TankDuel.Data;
using UnityEngine;

namespace TankDuel.Core
{
    /// <summary>
    /// ВРЕМЕННЫЙ: приёмка задачи 1.1. Прогоняет TankBuild через апгрейды и JSON,
    /// сверяет числа, пишет итог в консоль. После зелёного прогона снять с объекта и удалить файл.
    /// </summary>
    public class TankBuildSelfCheck : MonoBehaviour
    {
        void Start()
        {
            // Конфиги собираем в коде, чтобы проверка не зависела от ассетов
            var tank = ScriptableObject.CreateInstance<TankConfig>();
            tank.baseHealth = 100f;
            tank.baseDamage = 10f;
            tank.baseFireRate = 1f;
            tank.baseMoveSpeed = 5f;

            var upgrades = ScriptableObject.CreateInstance<UpgradeConfig>();
            upgrades.tracks = new[]
            {
                new UpgradeConfig.Track
                {
                    type = UpgradeType.Damage,
                    steps = new[]
                    {
                        new UpgradeConfig.Step { cost = 10, bonus = 5f },
                        new UpgradeConfig.Step { cost = 20, bonus = 5f },
                    },
                },
                new UpgradeConfig.Track
                {
                    type = UpgradeType.Health,
                    steps = new[] { new UpgradeConfig.Step { cost = 10, bonus = 25f } },
                },
            };

            var build = new TankBuild { hullId = "heavy", gunId = "sniper" };
            build.Upgrade(UpgradeType.Damage, upgrades);
            build.Upgrade(UpgradeType.Damage, upgrades);
            build.Upgrade(UpgradeType.Health, upgrades);

            // Туда-обратно через JSON — как поедет в сейв/по сети
            var restored = TankBuild.FromJson(build.ToJson());
            var stats = restored.ComputeStats(tank, upgrades);

            bool ok = true;
            ok &= Check("урон 20 после двух апгрейдов", stats.damage == 20f);
            ok &= Check("здоровье 125 после одного апгрейда", stats.maxHealth == 125f);
            ok &= Check("скорострельность без прокачки = 1", stats.fireRate == 1f);
            ok &= Check("уровень урона максимальный, дальше нельзя", !restored.CanUpgrade(UpgradeType.Damage, upgrades));
            ok &= Check("hullId пережил JSON", restored.hullId == "heavy");
            ok &= Check("цена след. уровня здоровья = -1 (максимум)", upgrades.GetCost(UpgradeType.Health, 1) == -1);

            Debug.Log(ok
                ? "<color=green>[TankBuild] Все проверки прошли — задача 1.1 принята. Этот компонент можно удалять.</color>"
                : "<color=red>[TankBuild] ЕСТЬ ПРОВАЛЫ — смотри логи выше.</color>");

            Destroy(tank);
            Destroy(upgrades);
        }

        bool Check(string label, bool passed)
        {
            if (!passed)
                Debug.LogError($"[TankBuild] ПРОВАЛ: {label}");
            return passed;
        }
    }
}

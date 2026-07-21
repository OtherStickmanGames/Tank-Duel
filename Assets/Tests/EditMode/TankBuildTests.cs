using NUnit.Framework;
using TankDuel.Data;
using UnityEngine;

namespace TankDuel.Tests
{
    public class TankBuildTests
    {
        TankConfig tank;
        UpgradeConfig upgrades;

        [SetUp]
        public void SetUp()
        {
            tank = ScriptableObject.CreateInstance<TankConfig>();
            tank.baseHealth = 100f;
            tank.baseDamage = 10f;
            tank.baseFireRate = 1f;
            tank.baseMoveSpeed = 5f;

            upgrades = ScriptableObject.CreateInstance<UpgradeConfig>();
            upgrades.tracks = new[]
            {
                MakeTrack(UpgradeType.Damage, (10, 5f), (20, 5f), (40, 10f)),
                MakeTrack(UpgradeType.FireRate, (10, 0.5f), (30, 0.5f)),
                MakeTrack(UpgradeType.MoveSpeed, (15, 1f)),
                MakeTrack(UpgradeType.Health, (10, 25f), (25, 25f)),
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(tank);
            Object.DestroyImmediate(upgrades);
        }

        static UpgradeConfig.Track MakeTrack(UpgradeType type, params (int cost, float bonus)[] steps)
        {
            var track = new UpgradeConfig.Track { type = type, steps = new UpgradeConfig.Step[steps.Length] };
            for (int i = 0; i < steps.Length; i++)
                track.steps[i] = new UpgradeConfig.Step { cost = steps[i].cost, bonus = steps[i].bonus };
            return track;
        }

        [Test]
        public void FreshBuild_StatsEqualBase()
        {
            var stats = new TankBuild().ComputeStats(tank, upgrades);

            Assert.AreEqual(100f, stats.maxHealth);
            Assert.AreEqual(10f, stats.damage);
            Assert.AreEqual(1f, stats.fireRate);
            Assert.AreEqual(5f, stats.moveSpeed);
        }

        [Test]
        public void Upgrade_IncreasesLevelAndStats()
        {
            var build = new TankBuild();

            Assert.IsTrue(build.Upgrade(UpgradeType.Damage, upgrades));
            Assert.IsTrue(build.Upgrade(UpgradeType.Damage, upgrades));

            Assert.AreEqual(2, build.GetLevel(UpgradeType.Damage));
            Assert.AreEqual(20f, build.ComputeStats(tank, upgrades).damage); // 10 + 5 + 5
            Assert.AreEqual(40, upgrades.GetCost(UpgradeType.Damage, 2));    // цена следующего уровня
        }

        [Test]
        public void Upgrade_StopsAtMaxLevel()
        {
            var build = new TankBuild();

            Assert.IsTrue(build.Upgrade(UpgradeType.MoveSpeed, upgrades));  // 0 -> 1, максимум
            Assert.IsFalse(build.CanUpgrade(UpgradeType.MoveSpeed, upgrades));
            Assert.IsFalse(build.Upgrade(UpgradeType.MoveSpeed, upgrades));
            Assert.AreEqual(1, build.GetLevel(UpgradeType.MoveSpeed));
        }

        [Test]
        public void GetCost_AtMaxLevel_ReturnsMinusOne()
        {
            Assert.AreEqual(15, upgrades.GetCost(UpgradeType.MoveSpeed, 0));
            Assert.AreEqual(-1, upgrades.GetCost(UpgradeType.MoveSpeed, 1));
            Assert.AreEqual(-1, upgrades.GetCost(UpgradeType.MoveSpeed, 99));
        }

        [Test]
        public void JsonRoundTrip_PreservesEverything()
        {
            var original = new TankBuild { hullId = "heavy", gunId = "sniper" };
            original.Upgrade(UpgradeType.Damage, upgrades);
            original.Upgrade(UpgradeType.Health, upgrades);
            original.Upgrade(UpgradeType.Health, upgrades);
            original.perks.Add(PerkType.None);

            var restored = TankBuild.FromJson(original.ToJson());

            Assert.AreEqual("heavy", restored.hullId);
            Assert.AreEqual("sniper", restored.gunId);
            Assert.AreEqual(1, restored.GetLevel(UpgradeType.Damage));
            Assert.AreEqual(2, restored.GetLevel(UpgradeType.Health));
            Assert.AreEqual(1, restored.perks.Count);

            var a = original.ComputeStats(tank, upgrades);
            var b = restored.ComputeStats(tank, upgrades);
            Assert.AreEqual(a.maxHealth, b.maxHealth);
            Assert.AreEqual(a.damage, b.damage);
            Assert.AreEqual(a.fireRate, b.fireRate);
            Assert.AreEqual(a.moveSpeed, b.moveSpeed);
        }

        [Test]
        public void FromJson_EmptyOrNull_ReturnsFreshBuild()
        {
            var fromEmpty = TankBuild.FromJson("");
            var fromNull = TankBuild.FromJson(null);

            Assert.AreEqual(0, fromEmpty.GetLevel(UpgradeType.Damage));
            Assert.AreEqual(0, fromNull.GetLevel(UpgradeType.Damage));
            Assert.IsNotNull(fromEmpty.perks);
            Assert.IsNotNull(fromNull.perks);
        }
    }
}

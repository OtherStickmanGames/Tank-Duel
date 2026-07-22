using System;
using System.Collections.Generic;
using TankDuel.Data;
using UnityEngine;

namespace TankDuel.Core
{
    /// <summary>
    /// Спавнит фарм-ботов и разрушаемые объекты в своей половине арены.
    /// Работает только в фазе Farm; на выходе из неё чистит всё живое.
    /// Одна и та же логика обслуживает половину игрока и половину оппонента —
    /// разница только в ownerTeam и положении зоны.
    /// </summary>
    public class Spawner : MonoBehaviour
    {
        [Header("Кому принадлежит половина: 0 — игрок (низ), 1 — оппонент (верх)")]
        public int ownerTeam;

        [Header("Зона спавна — прямоугольник вокруг этого объекта, метры")]
        public Vector2 zoneSize = new Vector2(16f, 18f);

        public WaveConfig waveConfig;

        class Active
        {
            public GameObject go;
            public Health health;
            public Action<Health> onDied;
            public int entryIndex;
        }

        readonly List<Active> active = new List<Active>();
        float[] nextSpawnTime;
        int[] spawnedTotal;
        bool spawning;
        float farmElapsed;

        // Танки не должны оказаться внутри заспавненного объекта
        Transform[] tanks;

        void Start()
        {
            var match = MatchController.Instance;
            if (match == null)
            {
                Debug.LogWarning($"{name}: MatchController не найден — спавнер не запустится.");
                return;
            }
            match.PhaseChanged += OnPhaseChanged;

            var controllers = FindObjectsByType<Tank.TankController>(FindObjectsSortMode.None);
            tanks = Array.ConvertAll(controllers, c => c.transform);
        }

        void OnDestroy()
        {
            if (MatchController.Instance != null)
                MatchController.Instance.PhaseChanged -= OnPhaseChanged;
        }

        void OnPhaseChanged(MatchPhase phase)
        {
            if (phase == MatchPhase.Farm)
                BeginFarm();
            else
                EndFarm();
        }

        void BeginFarm()
        {
            if (waveConfig == null || waveConfig.entries == null || waveConfig.entries.Length == 0)
            {
                Debug.LogWarning($"{name}: не назначен WaveConfig — спавнить нечего.");
                return;
            }

            int count = waveConfig.entries.Length;
            nextSpawnTime = new float[count];
            spawnedTotal = new int[count];
            for (int i = 0; i < count; i++)
                nextSpawnTime[i] = waveConfig.entries[i].startDelay;

            farmElapsed = 0f;
            spawning = true;
        }

        void EndFarm()
        {
            spawning = false;
            ClearAll();
        }

        void Update()
        {
            if (!spawning)
                return;

            farmElapsed += Time.deltaTime;

            var entries = waveConfig.entries;
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry?.prefab == null)
                    continue;
                if (farmElapsed < nextSpawnTime[i])
                    continue;
                if (entry.totalLimit > 0 && spawnedTotal[i] >= entry.totalLimit)
                    continue;
                if (CountAlive(i) >= entry.maxAlive)
                {
                    // Потолок живых — ждём следующий интервал, а не спамим попытками
                    nextSpawnTime[i] = farmElapsed + entry.interval;
                    continue;
                }

                if (TrySpawn(entry, i))
                    spawnedTotal[i]++;

                nextSpawnTime[i] = farmElapsed + Mathf.Max(0.05f, entry.interval);
            }
        }

        bool TrySpawn(WaveConfig.Entry entry, int entryIndex)
        {
            if (!TryFindFreePoint(entry, out var point))
                return false;

            var go = PoolManager.Get(entry.prefab, point, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));

            var health = go.GetComponent<Health>();
            if (health == null)
            {
                Debug.LogWarning($"{entry.prefab.name}: нет компонента Health — объект не убить, спавн отменён.");
                PooledObject.Despawn(go);
                return false;
            }

            // Цель фарма — враг для владельца половины, иначе игрок не сможет по ней стрелять
            health.team = 1 - ownerTeam;
            health.maxHealth = entry.health;
            health.ResetFull();

            var record = new Active { go = go, health = health, entryIndex = entryIndex };
            record.onDied = _ => OnTargetDied(record);
            health.Died += record.onDied;
            active.Add(record);

            return true;
        }

        // Несколько попыток найти точку, не занятую танком или другой целью
        bool TryFindFreePoint(WaveConfig.Entry entry, out Vector3 point)
        {
            const int attempts = 10;
            for (int i = 0; i < attempts; i++)
            {
                float x = UnityEngine.Random.Range(-zoneSize.x * 0.5f, zoneSize.x * 0.5f);
                float z = UnityEngine.Random.Range(-zoneSize.y * 0.5f, zoneSize.y * 0.5f);
                var candidate = transform.position + new Vector3(x, 0f, z);
                candidate.y = entry.spawnHeight;

                if (IsFree(candidate, entry.minSpacing))
                {
                    point = candidate;
                    return true;
                }
            }

            point = default;
            return false;
        }

        bool IsFree(Vector3 point, float spacing)
        {
            float sqrSpacing = spacing * spacing;

            if (tanks != null)
            {
                foreach (var tank in tanks)
                {
                    if (tank == null)
                        continue;
                    if (SqrDistanceXZ(tank.position, point) < sqrSpacing)
                        return false;
                }
            }

            foreach (var record in active)
            {
                if (record.go == null)
                    continue;
                if (SqrDistanceXZ(record.go.transform.position, point) < sqrSpacing)
                    return false;
            }

            return true;
        }

        static float SqrDistanceXZ(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        int CountAlive(int entryIndex)
        {
            int count = 0;
            foreach (var record in active)
            {
                if (record.entryIndex == entryIndex)
                    count++;
            }
            return count;
        }

        void OnTargetDied(Active record)
        {
            var entry = waveConfig.entries[record.entryIndex];
            ScoreSystem.Instance?.AddScore(ownerTeam, entry.scoreValue);

            Retire(record);
            active.Remove(record);
        }

        void ClearAll()
        {
            foreach (var record in active)
                Retire(record);
            active.Clear();
        }

        // Отписка обязательна: экземпляр вернётся из пула и подпишется заново
        void Retire(Active record)
        {
            if (record.health != null)
                record.health.Died -= record.onDied;
            PooledObject.Despawn(record.go);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = ownerTeam == 0 ? new Color(0.2f, 0.8f, 1f, 0.35f) : new Color(1f, 0.4f, 0.2f, 0.35f);
            Gizmos.DrawCube(transform.position, new Vector3(zoneSize.x, 0.1f, zoneSize.y));
        }
    }
}

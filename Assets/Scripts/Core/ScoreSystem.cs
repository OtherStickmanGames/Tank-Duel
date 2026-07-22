using System;
using UnityEngine;

namespace TankDuel.Core
{
    /// <summary>
    /// Очки за фарм, отдельно на каждую половину (0 — игрок, 1 — оппонент).
    /// Кто заработал очки, решает Spawner — владелец зоны, где умерла цель.
    /// Команда фарм-целей внутри зоны всегда совпадает (см. Spawner), так что
    /// зона однозначно определяет, чей это фарм — отдельно отслеживать,
    /// чей именно снаряд убил цель, не нужно.
    /// </summary>
    public class ScoreSystem : MonoBehaviour
    {
        public static ScoreSystem Instance { get; private set; }

        readonly int[] score = new int[2];

        public event Action<int, int> ScoreChanged; // (team, newScore)

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            var match = MatchController.Instance;
            if (match != null)
                match.PhaseChanged += OnPhaseChanged;
        }

        void OnDestroy()
        {
            if (MatchController.Instance != null)
                MatchController.Instance.PhaseChanged -= OnPhaseChanged;
            if (Instance == this)
                Instance = null;
        }

        void OnPhaseChanged(MatchPhase phase)
        {
            if (phase != MatchPhase.Warmup)
                return;

            // Рестарт матча — счёт обнуляется
            for (int team = 0; team < score.Length; team++)
            {
                score[team] = 0;
                ScoreChanged?.Invoke(team, 0);
            }
        }

        public int GetScore(int team) => score[team];

        public void AddScore(int team, int amount)
        {
            if (amount == 0)
                return;
            score[team] += amount;
            ScoreChanged?.Invoke(team, score[team]);
        }

        public bool TrySpend(int team, int amount)
        {
            if (score[team] < amount)
                return false;

            score[team] -= amount;
            ScoreChanged?.Invoke(team, score[team]);
            return true;
        }
    }
}

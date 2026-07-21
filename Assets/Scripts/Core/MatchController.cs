using System;
using UnityEngine;

namespace TankDuel.Core
{
    /// <summary>
    /// Оркестратор матча: единственное место, где меняются фазы.
    /// Никакой боевой логики внутри — остальные системы подписываются на PhaseChanged.
    /// На этапе 4 (сеть) фазы будет крутить только хост и реплицировать их клиенту —
    /// поэтому вся смена фаз проходит через одну точку SetPhase.
    /// </summary>
    public class MatchController : MonoBehaviour
    {
        public static MatchController Instance { get; private set; }

        [Header("Длительности фаз, сек")]
        public float warmupDuration = 3f;
        public float farmDuration = 60f;
        public float wallDropDuration = 1.5f;

        [Tooltip("Стартовать матч автоматически при запуске сцены")]
        public bool autoStart = true;

        public MatchPhase Phase { get; private set; } = MatchPhase.None;

        /// <summary>Сколько осталось текущей фазе. Для дуэли и результата не тикает.</summary>
        public float PhaseTimeRemaining { get; private set; }

        /// <summary>0 — игрок (низ), 1 — оппонент (верх), -1 — дуэль ещё не закончена.</summary>
        public int DuelWinner { get; private set; } = -1;

        /// <summary>Единственный источник правды о смене фаз. Подписывайтесь, а не опрашивайте в Update.</summary>
        public event Action<MatchPhase> PhaseChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Start()
        {
            if (autoStart)
                StartMatch();
        }

        public void StartMatch()
        {
            DuelWinner = -1;
            SetPhase(MatchPhase.Warmup, warmupDuration);
        }

        /// <summary>Вызывает боевая логика, когда один из танков умер.</summary>
        public void ReportDuelWinner(int winnerId)
        {
            if (Phase != MatchPhase.Duel)
                return;

            DuelWinner = winnerId;
            SetPhase(MatchPhase.Result, 0f);
        }

        /// <summary>Рестарт с экрана результата (кнопка «ещё раз»).</summary>
        public void RestartMatch()
        {
            if (Phase != MatchPhase.Result)
                return;

            StartMatch();
        }

        void Update()
        {
            // Тикают только фазы с таймером. Duel ждёт ReportDuelWinner, Result — RestartMatch.
            switch (Phase)
            {
                case MatchPhase.Warmup: Tick(MatchPhase.Farm, farmDuration); break;
                case MatchPhase.Farm: Tick(MatchPhase.WallDrop, wallDropDuration); break;
                case MatchPhase.WallDrop: Tick(MatchPhase.Duel, 0f); break;
            }
        }

        void Tick(MatchPhase next, float nextDuration)
        {
            PhaseTimeRemaining -= Time.deltaTime;
            if (PhaseTimeRemaining <= 0f)
                SetPhase(next, nextDuration);
        }

        void SetPhase(MatchPhase phase, float duration)
        {
            Phase = phase;
            PhaseTimeRemaining = duration;
            PhaseChanged?.Invoke(phase);
        }
    }
}

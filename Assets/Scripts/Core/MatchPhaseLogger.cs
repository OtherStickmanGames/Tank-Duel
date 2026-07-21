using UnityEngine;

namespace TankDuel.Core
{
    /// <summary>
    /// Отладка: пишет смену фаз в консоль. Живёт, пока нет нормального HUD (задача 3.2),
    /// потом удалить. Через контекстное меню компонента можно прогнать полную петлю
    /// без боевой логики: симулировать победу и рестарт.
    /// </summary>
    public class MatchPhaseLogger : MonoBehaviour
    {
        MatchController match;

        void OnEnable()
        {
            match = GetComponent<MatchController>();
            if (match != null)
                match.PhaseChanged += OnPhaseChanged;
        }

        void OnDisable()
        {
            if (match != null)
                match.PhaseChanged -= OnPhaseChanged;
        }

        void OnPhaseChanged(MatchPhase phase)
        {
            string extra = phase switch
            {
                MatchPhase.Result => $", победитель: {(match.DuelWinner == 0 ? "игрок" : "оппонент")}",
                MatchPhase.Duel => " (ждёт ReportDuelWinner — боевой логики пока нет, это нормально)",
                _ => match.PhaseTimeRemaining > 0f ? $", {match.PhaseTimeRemaining:0.#} сек" : "",
            };
            Debug.Log($"[Матч] Фаза: {phase}{extra}");
        }

        [ContextMenu("Симулировать победу игрока")]
        void SimulatePlayerWin() => match?.ReportDuelWinner(0);

        [ContextMenu("Симулировать победу оппонента")]
        void SimulateOpponentWin() => match?.ReportDuelWinner(1);

        [ContextMenu("Рестарт матча")]
        void Restart() => match?.RestartMatch();
    }
}

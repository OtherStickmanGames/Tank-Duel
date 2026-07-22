using TankDuel.Core;
using TankDuel.Data;
using TankDuel.Tank;
using UnityEngine;
using UnityEngine.UI;

namespace TankDuel.UI
{
    /// <summary>
    /// 4 кнопки прокачки. Доступна только в фазе Farm — вне неё кнопки
    /// неактивны, а не просто скрыты, чтобы игрок видел цены заранее.
    /// </summary>
    public class UpgradePanel : MonoBehaviour
    {
        public int team;
        public TankController tank;
        public UpgradeConfig upgradeConfig;

        public Button damageButton;
        public Button fireRateButton;
        public Button moveSpeedButton;
        public Button healthButton;
        public Text scoreText;

        (UpgradeType type, Button button, string label)[] slots;
        bool farmActive;

        void Awake()
        {
            slots = new[]
            {
                (UpgradeType.Damage, damageButton, "Урон"),
                (UpgradeType.FireRate, fireRateButton, "Скорострельность"),
                (UpgradeType.MoveSpeed, moveSpeedButton, "Скорость"),
                (UpgradeType.Health, healthButton, "Броня"),
            };

            foreach (var slot in slots)
            {
                var type = slot.type; // локальная копия для замыкания
                slot.button.onClick.AddListener(() => TryBuy(type));
            }
        }

        void Start()
        {
            var match = MatchController.Instance;
            if (match != null)
                match.PhaseChanged += OnPhaseChanged;

            var score = ScoreSystem.Instance;
            if (score != null)
                score.ScoreChanged += OnScoreChanged;

            farmActive = match != null && match.Phase == MatchPhase.Farm;
            Refresh();
        }

        void OnDestroy()
        {
            if (MatchController.Instance != null)
                MatchController.Instance.PhaseChanged -= OnPhaseChanged;
            if (ScoreSystem.Instance != null)
                ScoreSystem.Instance.ScoreChanged -= OnScoreChanged;
        }

        void OnPhaseChanged(MatchPhase phase)
        {
            farmActive = phase == MatchPhase.Farm;
            Refresh();
        }

        void OnScoreChanged(int changedTeam, int newScore)
        {
            if (changedTeam == team)
                Refresh();
        }

        void TryBuy(UpgradeType type)
        {
            if (!farmActive || tank == null || upgradeConfig == null)
                return;

            int cost = upgradeConfig.GetCost(type, tank.Build.GetLevel(type));
            if (cost < 0)
                return; // уровень уже максимальный

            if (!ScoreSystem.Instance.TrySpend(team, cost))
                return;

            tank.TryUpgrade(type);
            Refresh();
        }

        void Refresh()
        {
            int score = ScoreSystem.Instance != null ? ScoreSystem.Instance.GetScore(team) : 0;
            if (scoreText != null)
                scoreText.text = $"Очки: {score}";

            if (tank == null || upgradeConfig == null)
                return;

            foreach (var slot in slots)
            {
                int level = tank.Build.GetLevel(slot.type);
                int cost = upgradeConfig.GetCost(slot.type, level);
                var label = slot.button.GetComponentInChildren<Text>();

                if (cost < 0)
                {
                    if (label != null) label.text = $"{slot.label}\nМАКС";
                    slot.button.interactable = false;
                }
                else
                {
                    if (label != null) label.text = $"{slot.label}\n{cost}";
                    slot.button.interactable = farmActive && score >= cost;
                }
            }
        }
    }
}

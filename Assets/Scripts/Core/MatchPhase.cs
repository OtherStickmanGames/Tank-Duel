namespace TankDuel.Core
{
    /// <summary>Фазы матча. Порядок фиксированный: Warmup → Farm → WallDrop → Duel → Result.</summary>
    public enum MatchPhase
    {
        None = 0,     // сцена загружена, матч не начат
        Warmup = 1,   // отсчёт перед стартом
        Farm = 2,     // 60 секунд фарма и прокачки
        WallDrop = 3, // стена опускается
        Duel = 4,     // бой до смерти одного из танков
        Result = 5,   // экран результата, ждём рестарт
    }
}

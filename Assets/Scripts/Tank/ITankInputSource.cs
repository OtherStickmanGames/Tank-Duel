using UnityEngine;

namespace TankDuel.Tank
{
    /// <summary>
    /// Источник управления танком. TankController не знает, кто им рулит.
    /// Реализации: PlayerInputSource (клава+мышь), BotInputSource (задача 2.2),
    /// NetworkInputSource (этап 4). Висит на том же объекте, что и TankController.
    /// </summary>
    public interface ITankInputSource
    {
        /// <summary>Направление движения в мире (XZ), длина 0..1.</summary>
        Vector2 MoveInput { get; }

        /// <summary>Направление прицела башни в мире (XZ), нормализованное.</summary>
        Vector2 AimDirection { get; }

        /// <summary>Зажат ли огонь.</summary>
        bool IsFiring { get; }
    }
}

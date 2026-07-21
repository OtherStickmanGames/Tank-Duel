using UnityEngine;
using UnityEngine.InputSystem;

namespace TankDuel.Tank
{
    /// <summary>
    /// Клавиатура + мышь: WASD/стрелки — движение, курсор — прицел, ЛКМ или пробел — огонь.
    /// Тач-управление (два стика) — задача 6.3.
    /// </summary>
    public class PlayerInputSource : MonoBehaviour, ITankInputSource
    {
        public Vector2 MoveInput { get; private set; }
        public Vector2 AimDirection { get; private set; }
        public bool IsFiring { get; private set; }

        Camera cam;

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            var move = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
            }
            MoveInput = Vector2.ClampMagnitude(move, 1f);

            IsFiring = (mouse != null && mouse.leftButton.isPressed) ||
                       (kb != null && kb.spaceKey.isPressed);

            UpdateAim(mouse);
        }

        // Курсор → луч из камеры → плоскость на высоте танка → направление прицела
        void UpdateAim(Mouse mouse)
        {
            if (mouse == null)
                return;
            if (cam == null)
                cam = Camera.main;
            if (cam == null)
                return;

            var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            var plane = new Plane(Vector3.up, transform.position);
            if (plane.Raycast(ray, out float distance))
            {
                var point = ray.GetPoint(distance);
                var dir = new Vector2(point.x - transform.position.x, point.z - transform.position.z);
                if (dir.sqrMagnitude > 0.01f)
                    AimDirection = dir.normalized;
            }
        }
    }
}

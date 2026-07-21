using System.Collections;
using UnityEngine;

namespace TankDuel.Core
{
    /// <summary>
    /// Стена по центру арены. Пока цел коллайдер — физически блокирует танки
    /// (обычная столкновение Rigidbody) и снаряды (Projectile сам умирает
    /// об любой не-триггерный коллайдер, отдельного кода тут не нужно).
    /// На WallDrop опускается за MatchController.wallDropDuration, коллайдер
    /// выключается ровно в момент, когда открывается фаза Duel.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Wall : MonoBehaviour
    {
        [Tooltip("На сколько метров стена уходит вниз при опускании")]
        public float sinkDistance = 4f;

        Collider wallCollider;
        Vector3 closedPosition;
        Coroutine animation;

        void Awake()
        {
            wallCollider = GetComponent<Collider>();
            closedPosition = transform.position;
        }

        void Start()
        {
            var match = MatchController.Instance;
            if (match == null)
            {
                Debug.LogWarning($"{name}: MatchController не найден в сцене — стена не будет реагировать на фазы матча.");
                return;
            }
            match.PhaseChanged += OnPhaseChanged;
        }

        void OnDestroy()
        {
            if (MatchController.Instance != null)
                MatchController.Instance.PhaseChanged -= OnPhaseChanged;
        }

        void OnPhaseChanged(MatchPhase phase)
        {
            if (phase == MatchPhase.WallDrop)
            {
                Restart(AnimateDrop(MatchController.Instance.wallDropDuration));
            }
            else if (phase == MatchPhase.Warmup)
            {
                // Рестарт матча (кнопка «ещё раз») — стена возвращается на место
                Restart(null);
                transform.position = closedPosition;
                wallCollider.enabled = true;
            }
        }

        void Restart(IEnumerator routine)
        {
            if (animation != null)
                StopCoroutine(animation);
            animation = routine != null ? StartCoroutine(routine) : null;
        }

        IEnumerator AnimateDrop(float duration)
        {
            var start = transform.position;
            var end = start + Vector3.down * sinkDistance;

            if (duration <= 0f)
            {
                transform.position = end;
            }
            else
            {
                float t = 0f;
                while (t < duration)
                {
                    t += Time.deltaTime;
                    transform.position = Vector3.Lerp(start, end, t / duration);
                    yield return null;
                }
                transform.position = end;
            }

            // Выключаем коллайдер только когда стена реально ушла —
            // до этого момента она всё ещё физически на месте, даже если уже видно движение
            wallCollider.enabled = false;
            animation = null;
        }
    }
}

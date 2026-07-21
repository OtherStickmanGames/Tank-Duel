using UnityEngine;

namespace TankDuel.Core
{
    /// <summary>
    /// Стена по центру арены. Пока цел коллайдер — физически блокирует танки
    /// (обычная столкновение Rigidbody) и снаряды (Projectile сам умирает
    /// об любой не-триггерный коллайдер, отдельного кода тут не нужно).
    /// Двигается кинематическим Rigidbody.MovePosition в FixedUpdate — а не
    /// прямым присваиванием transform.position: коллайдер без Rigidbody,
    /// который телепортируется каждый кадр, физика считает статичной геометрией,
    /// внезапно сдвинутой. Если в этот момент танк упирается в стену, контакт
    /// ломается и в Rigidbody танка может влететь паразитный крутящий момент —
    /// именно так стена "закручивала" танк при опускании.
    /// Коллайдер выключается ровно в момент, когда стена реально ушла (не раньше).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public class Wall : MonoBehaviour
    {
        [Tooltip("На сколько метров стена уходит вниз при опускании")]
        public float sinkDistance = 4f;

        Collider wallCollider;
        Rigidbody body;
        Vector3 closedPosition;
        Vector3 openPosition;

        bool dropping;
        float dropStartTime;
        float dropDuration;

        void Awake()
        {
            wallCollider = GetComponent<Collider>();

            body = GetComponent<Rigidbody>();
            if (body == null)
                body = gameObject.AddComponent<Rigidbody>(); // на случай, если Wall стоял на объекте ещё до этого фикса
            body.isKinematic = true;
            body.useGravity = false;

            closedPosition = transform.position;
            openPosition = closedPosition + Vector3.down * sinkDistance;
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
                dropDuration = MatchController.Instance.wallDropDuration;
                dropStartTime = Time.time;
                dropping = true;
            }
            else if (phase == MatchPhase.Warmup)
            {
                // Рестарт матча (кнопка «ещё раз») — стена возвращается на место
                dropping = false;
                body.MovePosition(closedPosition);
                wallCollider.enabled = true;
            }
        }

        void FixedUpdate()
        {
            if (!dropping)
                return;

            float t = dropDuration > 0f ? (Time.time - dropStartTime) / dropDuration : 1f;
            if (t >= 1f)
            {
                body.MovePosition(openPosition);
                wallCollider.enabled = false; // стена реально ушла — до этого момента физически на месте
                dropping = false;
                return;
            }

            body.MovePosition(Vector3.Lerp(closedPosition, openPosition, t));
        }
    }
}

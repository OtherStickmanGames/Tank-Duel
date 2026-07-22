using UnityEngine;

namespace TankDuel.Tank
{
    /// <summary>
    /// Тупой ИИ фарм-бота: бродит по своей зоне, увидев цель — едет к ней и стреляет,
    /// пока та в радиусе видимости. Роль — быть кормом для прокачки игрока, не соперником.
    /// Цель и границы зоны назначает Spawner через Setup — сам бот никого не ищет:
    /// поиск «любого танка чужой команды» находил фарм-бота с соседней половины
    /// (у него команда владельца той зоны) и бот ехал к нему через стену.
    /// «Кусается редко» получаем не отдельным таймером здесь, а низким fireRate
    /// в конфиге бота — кулдаун выстрела уже считает сам TankController.
    /// </summary>
    public class BotInputSource : MonoBehaviour, ITankInputSource
    {
        [Header("Погоня")]
        public float sightRange = 9f;
        public float fireRange = 7f;
        public float stopDistance = 2f;

        [Header("Блуждание, когда цели не видно")]
        public float wanderRadius = 5f;
        public float wanderInterval = 2.5f;

        public Vector2 MoveInput { get; private set; }
        public Vector2 AimDirection { get; private set; } = Vector2.up;
        public bool IsFiring { get; private set; }

        Transform target;

        Vector3 zoneCenter;
        Vector2 zoneHalfExtents;
        bool hasZone;

        Vector3 homePosition;
        Vector3 wanderTarget;
        float nextWanderPick;

        /// <summary>
        /// Вызывает Spawner сразу после выдачи объекта из пула: за кем ехать и в каких
        /// границах бродить. Без зоны бот блуждает вокруг точки спавна — и на краю
        /// половины упирается в стену, поэтому зону передаём всегда.
        /// </summary>
        public void Setup(Transform target, Vector3 zoneCenter, Vector2 zoneSize)
        {
            this.target = target;
            this.zoneCenter = zoneCenter;
            zoneHalfExtents = zoneSize * 0.5f;
            hasZone = true;
        }

        void OnEnable()
        {
            // Объект мог вернуться из пула на новом месте — блуждание отталкиваем от него.
            // Цель обнуляем: её проставит Spawner следующим вызовом Setup.
            homePosition = transform.position;
            wanderTarget = homePosition;
            nextWanderPick = 0f;
            target = null;
        }

        void Update()
        {
            if (target != null && Vector3.Distance(transform.position, target.position) <= sightRange)
                ChaseAndShoot();
            else
                Wander();
        }

        void ChaseAndShoot()
        {
            var toTarget = target.position - transform.position;
            var flat = new Vector2(toTarget.x, toTarget.z);
            float distance = flat.magnitude;

            if (flat.sqrMagnitude > 0.0001f)
                AimDirection = flat.normalized;

            MoveInput = distance > stopDistance ? AimDirection : Vector2.zero;
            IsFiring = distance <= fireRange;
        }

        void Wander()
        {
            IsFiring = false;

            bool reached = new Vector2(wanderTarget.x - transform.position.x, wanderTarget.z - transform.position.z).magnitude < 0.5f;
            if (reached || Time.time >= nextWanderPick)
            {
                var offset = Random.insideUnitCircle * wanderRadius;
                wanderTarget = ClampToZone(homePosition + new Vector3(offset.x, 0f, offset.y));
                nextWanderPick = Time.time + wanderInterval;
            }

            var toWander = new Vector2(wanderTarget.x - transform.position.x, wanderTarget.z - transform.position.z);
            if (toWander.magnitude < 0.5f)
            {
                MoveInput = Vector2.zero;
                return;
            }

            AimDirection = toWander.normalized;
            MoveInput = toWander.normalized;
        }

        /// <summary>Держит точку блуждания внутри своей половины, чтобы бот не таранил стену.</summary>
        Vector3 ClampToZone(Vector3 point)
        {
            if (!hasZone)
                return point;

            point.x = Mathf.Clamp(point.x, zoneCenter.x - zoneHalfExtents.x, zoneCenter.x + zoneHalfExtents.x);
            point.z = Mathf.Clamp(point.z, zoneCenter.z - zoneHalfExtents.y, zoneCenter.z + zoneHalfExtents.y);
            return point;
        }
    }
}

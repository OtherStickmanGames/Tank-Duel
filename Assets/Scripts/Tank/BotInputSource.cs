using TankDuel.Core;
using UnityEngine;

namespace TankDuel.Tank
{
    /// <summary>
    /// Тупой ИИ фарм-бота: бродит, увидев вражеский танк — едет к нему и стреляет,
    /// пока тот в радиусе видимости. Роль — быть кормом для прокачки игрока,
    /// не соперником. Врага ищем среди TankController напрямую (в сцене их всего
    /// два — игрок и оппонент), без физических запросов.
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

        Health myHealth;
        Transform enemyTank;
        bool searchedEnemy;

        Vector3 homePosition;
        Vector3 wanderTarget;
        float nextWanderPick;

        void Awake()
        {
            myHealth = GetComponent<Health>();
        }

        void OnEnable()
        {
            // Объект мог вернуться из пула на новом месте — блуждание отталкиваем от него.
            // Врага ищем не здесь: в момент OnEnable у Spawner ещё не выставлен team (см. ниже).
            homePosition = transform.position;
            wanderTarget = homePosition;
            nextWanderPick = 0f;
            searchedEnemy = false;
            enemyTank = null;
        }

        void Update()
        {
            // Spawner выставляет Health.team сразу после того, как забрал объект из пула —
            // то есть уже после OnEnable, но до первого Update. Поэтому ищем врага здесь,
            // один раз за жизнь бота, а не в OnEnable.
            if (!searchedEnemy)
            {
                enemyTank = FindEnemyTank();
                searchedEnemy = true;
            }

            if (enemyTank != null && Vector3.Distance(transform.position, enemyTank.position) <= sightRange)
                ChaseAndShoot();
            else
                Wander();
        }

        void ChaseAndShoot()
        {
            var toEnemy = enemyTank.position - transform.position;
            var flat = new Vector2(toEnemy.x, toEnemy.z);
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
                wanderTarget = homePosition + new Vector3(offset.x, 0f, offset.y);
                nextWanderPick = Time.time + wanderInterval;
            }

            var toTarget = new Vector2(wanderTarget.x - transform.position.x, wanderTarget.z - transform.position.z);
            if (toTarget.magnitude < 0.5f)
            {
                MoveInput = Vector2.zero;
                return;
            }

            AimDirection = toTarget.normalized;
            MoveInput = toTarget.normalized;
        }

        Transform FindEnemyTank()
        {
            var tanks = FindObjectsByType<TankController>(FindObjectsSortMode.None);
            foreach (var tank in tanks)
            {
                var health = tank.Health;
                if (health != null && health.team != myHealth.team)
                    return tank.transform;
            }
            return null;
        }
    }
}

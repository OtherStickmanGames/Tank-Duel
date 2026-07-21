using TankDuel.Core;
using TankDuel.Data;
using UnityEngine;

namespace TankDuel.Tank
{
    /// <summary>
    /// Один контроллер на всех: игрок, бот, сетевой оппонент.
    /// Разница только в том, какой ITankInputSource висит рядом на объекте.
    /// Все числа — из TankStats (ApplyStats), в коде баланса нет.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Health))]
    public class TankController : MonoBehaviour
    {
        [Header("Конфиги: стартовые статы чистого билда. В матче статы обновляет прокачка через ApplyStats")]
        public TankConfig config;
        public UpgradeConfig upgradeConfig;

        [Header("Части танка")]
        public Transform turretPivot;
        public Transform firePoint;
        [Tooltip("Пусто — снаряд построится из примитива-сферы")]
        public Projectile projectilePrefab;

        [Header("Повороты, град/сек")]
        public float bodyTurnSpeed = 540f;
        public float turretTurnSpeed = 900f;

        public Health Health { get; private set; }
        public TankStats Stats { get; private set; }

        /// <summary>Гейт по фазам матча: снаружи выключается вне Farm/Duel (задача 2.x).</summary>
        public bool ControlsEnabled { get; set; } = true;

        ITankInputSource input;
        Rigidbody body;
        float nextFireTime;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            Health = GetComponent<Health>();
            input = GetComponent<ITankInputSource>();

            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezePositionY |
                               RigidbodyConstraints.FreezeRotationX |
                               RigidbodyConstraints.FreezeRotationZ;
        }

        void Start()
        {
            // Стартовые статы чистого билда, чтобы танк был играбелен сразу
            if (config != null && upgradeConfig != null)
                ApplyStats(new TankBuild().ComputeStats(config, upgradeConfig));
            else
                Debug.LogWarning($"{name}: не назначены конфиги — статы нулевые, танк не поедет");
        }

        public void ApplyStats(TankStats stats)
        {
            Stats = stats;
            Health.SetMax(stats.maxHealth);
        }

        void FixedUpdate()
        {
            bool canDrive = ControlsEnabled && !Health.IsDead && input != null;
            var move = canDrive ? input.MoveInput : Vector2.zero;

            body.linearVelocity = new Vector3(move.x, 0f, move.y) * Stats.moveSpeed;

            // Вращение — целиком на нашей стороне: гасим любой крутящий момент,
            // который физика могла вкинуть сама (нестабильный контакт, столкновение
            // с другим танком и т.п.), и поворачиваем только через MoveRotation ниже.
            body.angularVelocity = Vector3.zero;

            // Корпус доворачивается в сторону движения
            if (move.sqrMagnitude > 0.001f)
            {
                var target = Quaternion.LookRotation(new Vector3(move.x, 0f, move.y));
                body.MoveRotation(Quaternion.RotateTowards(body.rotation, target, bodyTurnSpeed * Time.fixedDeltaTime));
            }
        }

        void Update()
        {
            if (!ControlsEnabled || Health.IsDead || input == null)
                return;

            AimTurret();

            if (input.IsFiring && Stats.fireRate > 0f && Time.time >= nextFireTime)
            {
                nextFireTime = Time.time + 1f / Stats.fireRate;
                Fire();
            }
        }

        void AimTurret()
        {
            if (turretPivot == null)
                return;

            var aim = input.AimDirection;
            if (aim.sqrMagnitude < 0.001f)
                return;

            // Мировой угол напрямую — поворот корпуса прицел не сбивает
            float targetAngle = Mathf.Atan2(aim.x, aim.y) * Mathf.Rad2Deg;
            float angle = Mathf.MoveTowardsAngle(turretPivot.eulerAngles.y, targetAngle, turretTurnSpeed * Time.deltaTime);
            turretPivot.rotation = Quaternion.Euler(0f, angle, 0f);
        }

        void Fire()
        {
            var muzzle = firePoint != null ? firePoint : (turretPivot != null ? turretPivot : transform);
            float projectileSpeed = config != null ? config.projectileSpeed : 20f;
            float projectileLifetime = config != null ? config.projectileLifetime : 3f;

            Projectile.Spawn(projectilePrefab, muzzle.position, muzzle.forward,
                projectileSpeed, projectileLifetime, Stats.damage, Health.team);
        }
    }
}

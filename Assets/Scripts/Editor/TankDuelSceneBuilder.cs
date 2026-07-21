using TankDuel.Core;
using TankDuel.Data;
using TankDuel.Tank;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TankDuel.EditorTools
{
    /// <summary>
    /// Генераторы объектов сцены (решение №8 в плане): всё, что нужно в сцене,
    /// создаётся отсюда через меню Tank Duel, а не руками в иерархии.
    /// Все генераторы идемпотентны: повторный запуск обновляет существующее, дублей не плодит.
    /// </summary>
    public static class TankDuelSceneBuilder
    {
        const string TankConfigPath = "Assets/ScriptableObjects/TankConfig.asset";
        const string UpgradeConfigPath = "Assets/ScriptableObjects/UpgradeConfig.asset";
        const string WaveConfigPath = "Assets/ScriptableObjects/WaveConfig.asset";

        const string FarmBotPrefabPath = "Assets/Prefabs/FarmBot.prefab";
        const string CratePrefabPath = "Assets/Prefabs/Crate.prefab";
        const string ProjectilePrefabPath = "Assets/Prefabs/Projectile.prefab";

        [MenuItem("Tank Duel/Build Match Core")]
        public static void BuildMatchCore()
        {
            var go = GameObject.Find("Match");
            if (go == null)
            {
                go = new GameObject("Match");
                Undo.RegisterCreatedObjectUndo(go, "Build Match Core");
            }

            EnsureComponent<MatchController>(go);
            EnsureComponent<MatchPhaseLogger>(go);

            // Самолечение: подчищаем обломки удалённых скриптов (например, TankBuildSelfCheck)
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);

            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("[Tank Duel] Match Core собран/обновлён: объект «Match» в сцене.");
        }

        // ---------- Задача 1.3: полигон для обкатки танка ----------

        [MenuItem("Tank Duel/Build Test Range")]
        public static void BuildTestRange()
        {
            CreateDefaultConfigs(); // танку нужны конфиги

            // Земля
            var ground = GameObject.Find("Ground");
            if (ground == null)
            {
                ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground";
                Undo.RegisterCreatedObjectUndo(ground, "Build Test Range");
            }
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(4f, 1f, 4f); // 40x40 метров

            // Камера сверху с наклоном
            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(0f, 24f, -18f);
                cam.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
            }

            // Танк игрока
            var tank = GameObject.Find("PlayerTank");
            if (tank == null)
                tank = BuildTankObject("PlayerTank", team: 0, withPlayerInput: true);
            tank.transform.position = new Vector3(0f, 0.31f, -8f);

            // Мишени: пересоздаются с нуля при каждом запуске
            var targetsRoot = GameObject.Find("Targets");
            if (targetsRoot != null)
                Object.DestroyImmediate(targetsRoot);
            targetsRoot = new GameObject("Targets");
            Undo.RegisterCreatedObjectUndo(targetsRoot, "Build Test Range");

            var positions = new[]
            {
                new Vector3(-6f, 1f, 2f),
                new Vector3(0f, 1f, 6f),
                new Vector3(6f, 1f, 2f),
                new Vector3(-3f, 1f, 12f),
                new Vector3(3f, 1f, 12f),
            };
            foreach (var pos in positions)
            {
                var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.name = "Target";
                capsule.transform.SetParent(targetsRoot.transform, false);
                capsule.transform.position = pos;

                var health = capsule.AddComponent<Health>();
                health.maxHealth = 30f;
                health.team = 1;
                capsule.AddComponent<DestroyOnDeath>();
            }

            EditorSceneManager.MarkSceneDirty(ground.scene);
            Debug.Log("[Tank Duel] Test Range собран: танк на WASD, прицел мышью, огонь ЛКМ/пробел. Мишени по 30 HP.");
        }

        /// <summary>Танк из примитивов. Станет префабом, когда появится арт вместо грейбокса.</summary>
        static GameObject BuildTankObject(string name, int team, bool withPlayerInput)
        {
            var root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, "Build Tank");

            // Визуал: корпус
            var bodyVisual = CreateVisual(PrimitiveType.Cube, "Body", root.transform,
                Vector3.zero, new Vector3(1.8f, 0.6f, 2.6f));

            // Башня на пивоте — контроллер вращает пивот, не визуал
            var pivot = new GameObject("TurretPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(0f, 0.55f, 0f);

            CreateVisual(PrimitiveType.Cube, "Turret", pivot,
                Vector3.zero, new Vector3(1.1f, 0.45f, 1.1f));
            CreateVisual(PrimitiveType.Cube, "Barrel", pivot,
                new Vector3(0f, 0f, 1f), new Vector3(0.25f, 0.25f, 1.4f));

            var firePoint = new GameObject("FirePoint").transform;
            firePoint.SetParent(pivot, false);
            firePoint.localPosition = new Vector3(0f, 0f, 1.8f);

            // Физика: один коллайдер на руте
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(2f, 1f, 2.8f);
            box.center = new Vector3(0f, 0.2f, 0f);

            // Rigidbody и Health добавятся сами через RequireComponent
            var controller = root.AddComponent<TankController>();
            controller.turretPivot = pivot;
            controller.firePoint = firePoint;
            controller.config = AssetDatabase.LoadAssetAtPath<TankConfig>(TankConfigPath);
            controller.upgradeConfig = AssetDatabase.LoadAssetAtPath<UpgradeConfig>(UpgradeConfigPath);

            var projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath);
            if (projectilePrefab != null)
                controller.projectilePrefab = projectilePrefab.GetComponent<Projectile>();

            root.GetComponent<Health>().team = team;
            root.GetComponent<Rigidbody>().useGravity = false;

            if (withPlayerInput)
                root.AddComponent<PlayerInputSource>();

            return root;
        }

        static GameObject CreateVisual(PrimitiveType type, string name, Transform parent,
            Vector3 localPos, Vector3 localScale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // визуал без коллайдера
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            return go;
        }

        // ---------- Задача 1.4: арена и стена ----------

        const float ArenaHalfDepth = 25f; // длина одной половины по Z
        const float ArenaWidth = 20f;     // ширина арены по X
        const float WallHeight = 3f;
        const float WallThickness = 1f;

        [MenuItem("Tank Duel/Build Arena")]
        public static void BuildArena()
        {
            BuildMatchCore();       // стене и танкам нужен MatchController в сцене
            CreateDefaultConfigs(); // танкам — конфиги, спавнерам — волны и префабы целей

            // Земля на всю арену: от -ArenaHalfDepth до +ArenaHalfDepth по Z.
            // Общий объект с Build Test Range — тот же принцип идемпотентности,
            // просто подгоняем существующую землю под размеры арены.
            var ground = GameObject.Find("Ground");
            if (ground == null)
            {
                ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground";
                Undo.RegisterCreatedObjectUndo(ground, "Build Arena");
            }
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(ArenaWidth / 10f, 1f, (ArenaHalfDepth * 2f) / 10f);

            // Стена по центру: делит арену на половину игрока (Z<0) и оппонента (Z>0)
            var wallGo = GameObject.Find("Wall");
            if (wallGo == null)
            {
                wallGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wallGo.name = "Wall";
                Undo.RegisterCreatedObjectUndo(wallGo, "Build Arena");
            }
            wallGo.transform.position = new Vector3(0f, WallHeight / 2f, 0f);
            wallGo.transform.localScale = new Vector3(ArenaWidth + 0.5f, WallHeight, WallThickness);
            EnsureComponent<Wall>(wallGo);

            // Точки спавна для дуэли
            var spawnRoot = GameObject.Find("SpawnPoints");
            if (spawnRoot == null)
            {
                spawnRoot = new GameObject("SpawnPoints");
                Undo.RegisterCreatedObjectUndo(spawnRoot, "Build Arena");
            }
            var playerSpawn = EnsureChild(spawnRoot.transform, "PlayerSpawn", new Vector3(0f, 0f, -ArenaHalfDepth + 5f));
            var opponentSpawn = EnsureChild(spawnRoot.transform, "OpponentSpawn", new Vector3(0f, 0f, ArenaHalfDepth - 5f));

            // Танки на точках спавна. Оппонент пока без входа — ИИ приедет в 2.4/3.1,
            // сейчас это просто мишень для проверки стены с той стороны.
            var playerTank = GameObject.Find("PlayerTank");
            if (playerTank == null)
                playerTank = BuildTankObject("PlayerTank", team: 0, withPlayerInput: true);
            playerTank.transform.position = playerSpawn.position + Vector3.up * 0.31f;
            playerTank.transform.rotation = Quaternion.identity;

            var opponentTank = GameObject.Find("OpponentTank");
            if (opponentTank == null)
                opponentTank = BuildTankObject("OpponentTank", team: 1, withPlayerInput: false);
            opponentTank.transform.position = opponentSpawn.position + Vector3.up * 0.31f;
            opponentTank.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // лицом к игроку

            // Спавнеры: по одному на половину, зона чуть уже арены и не упирается в стену
            var waveConfig = AssetDatabase.LoadAssetAtPath<WaveConfig>(WaveConfigPath);
            BuildSpawner("PlayerSpawner", ownerTeam: 0, center: new Vector3(0f, 0f, -13f), waveConfig);
            BuildSpawner("OpponentSpawner", ownerTeam: 1, center: new Vector3(0f, 0f, 13f), waveConfig);

            // Камера сверху с наклоном, чтобы обе половины были в кадре
            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(0f, 42f, -30f);
                cam.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
            }

            EditorSceneManager.MarkSceneDirty(wallGo.scene);
            Debug.Log("[Tank Duel] Arena собрана: стена, спавн-поинты, два спавнера, танки. " +
                      "Для быстрой проверки поставь farmDuration на MatchController в 10-15 сек.");
        }

        static void BuildSpawner(string name, int ownerTeam, Vector3 center, WaveConfig waveConfig)
        {
            var go = GameObject.Find(name);
            if (go == null)
            {
                go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "Build Arena");
            }
            go.transform.position = center;

            var spawner = EnsureComponent<Spawner>(go);
            spawner.ownerTeam = ownerTeam;
            spawner.zoneSize = new Vector2(ArenaWidth - 4f, ArenaHalfDepth - 8f);
            spawner.waveConfig = waveConfig;
        }

        static Transform EnsureChild(Transform parent, string name, Vector3 localPosition)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                var go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "Build Arena");
                child = go.transform;
                child.SetParent(parent, false);
            }
            child.localPosition = localPosition;
            return child;
        }

        // ---------- Утилиты ----------

        /// <summary>Снимает «Missing Script» со всех объектов открытых сцен. Нужен после удаления временных скриптов.</summary>
        [MenuItem("Tank Duel/Remove Missing Scripts In Scene")]
        public static void RemoveMissingScriptsInScene()
        {
            int removed = 0;
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                    if (count > 0)
                    {
                        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                        removed += count;
                    }
                }
            }

            if (removed > 0)
                EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[Tank Duel] Снято обломков Missing Script: {removed}.");
        }

        // ---------- Префабы целей и снаряда ----------

        [MenuItem("Tank Duel/Create Default Prefabs")]
        public static void CreateDefaultPrefabs()
        {
            // Фарм-бот: пока статичная мишень, ИИ приедет в 2.2 отдельным компонентом
            CreatePrefabIfMissing(FarmBotPrefabPath, () =>
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = "FarmBot";
                go.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
                go.AddComponent<Health>().maxHealth = 30f;
                return go;
            });

            // Разрушаемый ящик
            CreatePrefabIfMissing(CratePrefabPath, () =>
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Crate";
                go.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
                go.AddComponent<Health>().maxHealth = 20f;
                return go;
            });

            // Снаряд: триггер + кинематика, двигает его сам Projectile
            CreatePrefabIfMissing(ProjectilePrefabPath, () =>
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Projectile";
                go.transform.localScale = Vector3.one * 0.35f;
                go.GetComponent<Collider>().isTrigger = true;
                go.AddComponent<Projectile>(); // Rigidbody придёт через RequireComponent
                go.GetComponent<Rigidbody>().isKinematic = true;
                return go;
            });

            AssetDatabase.SaveAssets();
        }

        static void CreatePrefabIfMissing(string path, System.Func<GameObject> build)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                return;

            var temp = build();
            PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            Debug.Log($"[Tank Duel] Создан префаб: {path}");
        }

        // ---------- Конфиги ----------

        [MenuItem("Tank Duel/Create Default Configs")]
        public static void CreateDefaultConfigs()
        {
            CreateIfMissing<TankConfig>(TankConfigPath, null);
            CreateIfMissing<UpgradeConfig>(UpgradeConfigPath, config =>
            {
                // Стартовый баланс — крутить в инспекторе, не здесь
                config.tracks = new[]
                {
                    Track(UpgradeType.Damage, (10, 5f), (20, 5f), (40, 10f)),
                    Track(UpgradeType.FireRate, (10, 0.5f), (30, 0.5f)),
                    Track(UpgradeType.MoveSpeed, (15, 1f), (35, 1f)),
                    Track(UpgradeType.Health, (10, 25f), (25, 25f), (50, 50f)),
                };
            });

            CreateDefaultPrefabs(); // конфигу волн нужны ссылки на префабы

            CreateIfMissing<WaveConfig>(WaveConfigPath, config =>
            {
                config.entries = new[]
                {
                    new WaveConfig.Entry
                    {
                        label = "Боты",
                        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FarmBotPrefabPath),
                        startDelay = 0f,
                        interval = 2f,
                        maxAlive = 6,
                        health = 30f,
                        spawnHeight = 1f,
                    },
                    new WaveConfig.Entry
                    {
                        label = "Ящики",
                        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CratePrefabPath),
                        startDelay = 1f,
                        interval = 3f,
                        maxAlive = 8,
                        health = 20f,
                        spawnHeight = 0.6f,
                    },
                };
            });

            AssetDatabase.SaveAssets();
        }

        static UpgradeConfig.Track Track(UpgradeType type, params (int cost, float bonus)[] steps)
        {
            var track = new UpgradeConfig.Track { type = type, steps = new UpgradeConfig.Step[steps.Length] };
            for (int i = 0; i < steps.Length; i++)
                track.steps[i] = new UpgradeConfig.Step { cost = steps[i].cost, bonus = steps[i].bonus };
            return track;
        }

        static void CreateIfMissing<T>(string path, System.Action<T> init) where T : ScriptableObject
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null)
                return;

            var asset = ScriptableObject.CreateInstance<T>();
            init?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
            Debug.Log($"[Tank Duel] Создан: {path}");
        }

        static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            if (component == null)
                component = Undo.AddComponent<T>(go);
            return component;
        }
    }
}

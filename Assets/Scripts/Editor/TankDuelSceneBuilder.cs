using TankDuel.Core;
using TankDuel.Data;
using TankDuel.Tank;
using TankDuel.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

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
        const string FarmBotConfigPath = "Assets/ScriptableObjects/FarmBotConfig.asset";

        const string FarmBotPrefabPath = "Assets/Prefabs/FarmBot.prefab";
        const string CratePrefabPath = "Assets/Prefabs/Crate.prefab";
        const string ProjectilePrefabPath = "Assets/Prefabs/Projectile.prefab";

        // ---------- Цвет вместо серого грейбокса ----------
        // Палитра временная — заменится артом, но пока хотя бы читаемо кто есть кто.
        const string MatDir = "Assets/Art/Materials/";
        static readonly Color PlayerColor = new Color(0.25f, 0.55f, 0.95f);   // синий — игрок
        static readonly Color OpponentColor = new Color(0.9f, 0.25f, 0.25f);  // красный — оппонент
        static readonly Color WallColor = new Color(0.95f, 0.75f, 0.15f);     // жёлтый — барьер, нейтральный
        static readonly Color FarmBotColor = new Color(0.95f, 0.55f, 0.15f);  // оранжевый — фарм-цель
        static readonly Color CrateColor = new Color(0.55f, 0.4f, 0.22f);     // коричневый — ящик
        static readonly Color GroundColor = new Color(0.32f, 0.38f, 0.28f);   // тёмно-оливковый — земля
        static readonly Color ProjectileColor = new Color(1f, 0.92f, 0.35f);  // ярко-жёлтый — снаряд, видно на земле

        static Material GetOrCreateMaterial(string path, Color color)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(path),
                };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color; // держим цвет в синхроне на случай ручных правок палитры выше
            EditorUtility.SetDirty(mat);
            return mat;
        }

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
            EnsureComponent<ScoreSystem>(go);

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
            ground.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial(MatDir + "Mat_Ground.mat", GroundColor);

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
            ApplyTankMaterial(tank, team: 0);
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

            var tankMaterial = team == 0
                ? GetOrCreateMaterial(MatDir + "Mat_PlayerTank.mat", PlayerColor)
                : GetOrCreateMaterial(MatDir + "Mat_OpponentTank.mat", OpponentColor);

            // Визуал: корпус
            var bodyVisual = CreateVisual(PrimitiveType.Cube, "Body", root.transform,
                Vector3.zero, new Vector3(1.8f, 0.6f, 2.6f), tankMaterial);

            // Башня на пивоте — контроллер вращает пивот, не визуал
            var pivot = new GameObject("TurretPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(0f, 0.55f, 0f);

            CreateVisual(PrimitiveType.Cube, "Turret", pivot,
                Vector3.zero, new Vector3(1.1f, 0.45f, 1.1f), tankMaterial);
            CreateVisual(PrimitiveType.Cube, "Barrel", pivot,
                new Vector3(0f, 0f, 1f), new Vector3(0.25f, 0.25f, 1.4f), tankMaterial);

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
            Vector3 localPos, Vector3 localScale, Material material = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // визуал без коллайдера
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            if (material != null)
                go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>
        /// Перекрашивает готовый танк (Body/Turret/Barrel), не пересобирая его.
        /// Нужен отдельно от BuildTankObject: идемпотентные генераторы пропускают
        /// уже существующий танк по имени, значит его материал сам не обновится.
        /// </summary>
        static void ApplyTankMaterial(GameObject tank, int team)
        {
            var material = team == 0
                ? GetOrCreateMaterial(MatDir + "Mat_PlayerTank.mat", PlayerColor)
                : GetOrCreateMaterial(MatDir + "Mat_OpponentTank.mat", OpponentColor);

            foreach (var path in new[] { "Body", "TurretPivot/Turret", "TurretPivot/Barrel" })
            {
                var renderer = tank.transform.Find(path)?.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.sharedMaterial = material;
            }
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
            ground.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial(MatDir + "Mat_Ground.mat", GroundColor);

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
            wallGo.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial(MatDir + "Mat_Wall.mat", WallColor);
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
            ApplyTankMaterial(playerTank, team: 0);
            playerTank.transform.position = playerSpawn.position + Vector3.up * 0.31f;
            playerTank.transform.rotation = Quaternion.identity;

            var opponentTank = GameObject.Find("OpponentTank");
            if (opponentTank == null)
                opponentTank = BuildTankObject("OpponentTank", team: 1, withPlayerInput: false);
            ApplyTankMaterial(opponentTank, team: 1);
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

            BuildHud(); // панель прокачки игрока — 2.3

            EditorSceneManager.MarkSceneDirty(wallGo.scene);
            Debug.Log("[Tank Duel] Arena собрана: стена, спавн-поинты, два спавнера, танки, HUD. " +
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

        // ---------- Задача 2.3: HUD с прокачкой ----------

        [MenuItem("Tank Duel/Build HUD")]
        public static void BuildHud()
        {
            BuildMatchCore(); // панели нужны ScoreSystem и MatchController в сцене

            if (!EnsureTmpEssentials())
                return; // ассеты ещё едут — HUD соберётся со следующего нажатия

            var font = EnsureFontAsset();
            EnsureEventSystem();

            var hudGo = GameObject.Find("HUD");
            if (hudGo == null)
            {
                hudGo = new GameObject("HUD");
                Undo.RegisterCreatedObjectUndo(hudGo, "Build HUD");
            }

            var canvas = EnsureComponent<Canvas>(hudGo);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = EnsureComponent<CanvasScaler>(hudGo);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            EnsureComponent<GraphicRaycaster>(hudGo);

            var hudRect = hudGo.GetComponent<RectTransform>();

            // Панель прокачки — низ экрана
            var panelRect = EnsureUiChild(hudRect, "UpgradePanel");
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 30f);
            panelRect.sizeDelta = new Vector2(720f, 90f);

            var layout = EnsureComponent<HorizontalLayoutGroup>(panelRect.gameObject);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var damageButton = BuildUpgradeButton(panelRect, "DamageButton", font);
            var fireRateButton = BuildUpgradeButton(panelRect, "FireRateButton", font);
            var moveSpeedButton = BuildUpgradeButton(panelRect, "MoveSpeedButton", font);
            var healthButton = BuildUpgradeButton(panelRect, "HealthButton", font);

            // Счёт — верх экрана
            var scoreRect = EnsureUiChild(hudRect, "ScoreText");
            scoreRect.anchorMin = new Vector2(0.5f, 1f);
            scoreRect.anchorMax = new Vector2(0.5f, 1f);
            scoreRect.pivot = new Vector2(0.5f, 1f);
            scoreRect.anchoredPosition = new Vector2(0f, -30f);
            scoreRect.sizeDelta = new Vector2(400f, 60f);

            var scoreText = EnsureTmpText(scoreRect, font, fontSize: 32f);
            scoreText.text = "Очки: 0";

            var upgradePanel = EnsureComponent<UpgradePanel>(panelRect.gameObject);
            upgradePanel.team = 0;
            upgradePanel.tank = GameObject.Find("PlayerTank")?.GetComponent<TankController>();
            upgradePanel.upgradeConfig = AssetDatabase.LoadAssetAtPath<UpgradeConfig>(UpgradeConfigPath);
            upgradePanel.damageButton = damageButton;
            upgradePanel.fireRateButton = fireRateButton;
            upgradePanel.moveSpeedButton = moveSpeedButton;
            upgradePanel.healthButton = healthButton;
            upgradePanel.scoreText = scoreText;

            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(hudGo);

            EditorSceneManager.MarkSceneDirty(hudGo.scene);
            Debug.Log("[Tank Duel] HUD собран: панель прокачки (низ, активна только в фазе Farm) + счёт (верх).");
        }

        /// <summary>Единственный EventSystem на сцену, с модулем нового Input System вместо легаси.</summary>
        static void EnsureEventSystem()
        {
            var existing = Object.FindFirstObjectByType<EventSystem>();
            if (existing == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                Undo.RegisterCreatedObjectUndo(go, "Build HUD");
                return;
            }

            if (existing.GetComponent<InputSystemUIInputModule>() == null)
                existing.gameObject.AddComponent<InputSystemUIInputModule>();

            var legacyModule = existing.GetComponent<StandaloneInputModule>();
            if (legacyModule != null)
                Object.DestroyImmediate(legacyModule);
        }

        const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        const string TmpSourceFontPath = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";
        const string FontAssetPath = "Assets/Art/Fonts/TankDuel SDF.asset";

        /// <summary>
        /// TMP не работает без TMP Essential Resources, а их обычно импортируют руками
        /// через диалог. Тянем их программно — иначе пункт меню собирает HUD с пустыми
        /// надписями. Метод импортёра внутренний, поэтому через рефлексию: если TMP
        /// сменит API, генератор не развалится, а просто попросит нажать пункт меню.
        /// false — ассетов ещё нет, сборку HUD надо отложить до следующего запуска.
        /// </summary>
        static bool EnsureTmpEssentials()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath) != null)
                return true; // уже импортированы

            const string manualHint = "[Tank Duel] Не удалось импортировать TMP Essentials автоматически. " +
                                      "Нажми Window → TextMeshPro → Import TMP Essential Resources и повтори Build HUD.";

            // Имя сборки у TMP переезжало между версиями пакета — ищем тип по всем загруженным
            System.Type importer = null;
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                importer = assembly.GetType("TMPro.TMP_PackageResourceImporter");
                if (importer != null)
                    break;
            }

            var method = importer?.GetMethod("ImportResources",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static);

            if (method == null)
            {
                Debug.LogWarning(manualHint);
                return false;
            }

            try
            {
                // Первый аргумент — essentials, остальные (examples/extras) не нужны
                var parameters = method.GetParameters();
                var args = new object[parameters.Length];
                for (int i = 0; i < args.Length; i++)
                    args[i] = i == 0;

                method.Invoke(null, args);
                AssetDatabase.Refresh();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{manualHint}\nПричина: {e.Message}");
                return false;
            }

            // ImportPackage кладёт файлы в очередь, а не разом: в этом же вызове меню
            // ассетов ещё нет, и компоненты получились бы без шрифта. Проверяем и,
            // если не успели, просим нажать пункт меню повторно.
            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath) != null)
                return true;

            Debug.Log("[Tank Duel] Запущен импорт TMP Essential Resources. " +
                      "Дождись окончания импорта и нажми Build HUD ещё раз.");
            return false;
        }

        /// <summary>
        /// Шрифт с кириллицей. Штатный «LiberationSans SDF» из TMP Essentials собран
        /// со статическим ASCII-атласом — русские надписи в нём превращаются в квадраты.
        /// Поэтому делаем свой ассет из той же TTF, но с динамическим атласом:
        /// глифы дорисовываются по мере надобности, кириллица включительно.
        /// </summary>
        static TMP_FontAsset EnsureFontAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
                return existing;

            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(TmpSourceFontPath);
            if (sourceFont == null)
            {
                Debug.LogWarning($"[Tank Duel] Не найден {TmpSourceFontPath} — надписи останутся без шрифта.");
                return null;
            }

            EnsureFolder("Assets/Art", "Fonts");

            // Аргументы позиционные: размер сэмплирования, паддинг, режим рендера,
            // ширина и высота атласа, режим наполнения, мультиатлас
            var fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                48,
                5,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic,
                true);

            if (fontAsset == null)
            {
                Debug.LogWarning("[Tank Duel] TMP не смог собрать шрифт из LiberationSans.ttf.");
                return null;
            }

            fontAsset.name = "TankDuel SDF";
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

            // Атлас и материал живут внутри ассета шрифта, иначе после перезапуска
            // редактора ссылки на них обнуляются и текст снова пропадает
            if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0)
            {
                fontAsset.atlasTextures[0].name = "TankDuel SDF Atlas";
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            }
            if (fontAsset.material != null)
            {
                fontAsset.material.name = "TankDuel SDF Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Tank Duel] Создан шрифт с кириллицей: {FontAssetPath}");
            return fontAsset;
        }

        static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
                AssetDatabase.CreateFolder(parent, child);
        }

        /// <summary>Идемпотентный TMP-текст на готовом RectTransform.</summary>
        static TextMeshProUGUI EnsureTmpText(RectTransform rect, TMP_FontAsset font, float fontSize)
        {
            // Миграция с легаси UGUI: Text и TextMeshProUGUI оба Graphic, вдвоём на одном
            // объекте не уживаются. У кого HUD собран прошлой версией генератора — снимаем старый.
            var legacy = rect.GetComponent<Text>();
            if (legacy != null)
                Object.DestroyImmediate(legacy);

            var text = rect.GetComponent<TextMeshProUGUI>();
            if (text == null)
                text = rect.gameObject.AddComponent<TextMeshProUGUI>();

            // Назначаем явно: дефолтный шрифт из TMP Settings не только приезжает
            // с задержкой после импорта, но ещё и без кириллицы
            if (font != null)
                text.font = font;

            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = fontSize;
            text.color = Color.white;
            return text;
        }

        /// <summary>Идемпотентный RectTransform-ребёнок: находит по имени или создаёт новый.</summary>
        static RectTransform EnsureUiChild(Transform parent, string name)
        {
            var child = parent.Find(name) as RectTransform;
            if (child == null)
            {
                var go = new GameObject(name, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "Build HUD");
                child = go.GetComponent<RectTransform>();
                child.SetParent(parent, false);
            }
            return child;
        }

        static Button BuildUpgradeButton(Transform parent, string name, TMP_FontAsset font)
        {
            var rect = EnsureUiChild(parent, name);
            rect.sizeDelta = new Vector2(160f, 80f);

            var image = rect.GetComponent<Image>();
            if (image == null)
                image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);

            var button = rect.GetComponent<Button>();
            if (button == null)
                button = rect.gameObject.AddComponent<Button>();

            var labelRect = EnsureUiChild(rect, "Label");
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            EnsureTmpText(labelRect, font, fontSize: 16f);

            return button;
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
            // Фарм-бот: тот же TankController, что у игрока, — просто с BotInputSource
            // вместо PlayerInputSource. В этом весь смысл абстракции ITankInputSource из 1.3.
            CreatePrefabIfMissing(FarmBotPrefabPath, () =>
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = "FarmBot";
                go.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);

                var controller = go.AddComponent<TankController>(); // Rigidbody и Health придут через RequireComponent
                controller.config = AssetDatabase.LoadAssetAtPath<TankConfig>(FarmBotConfigPath);
                controller.upgradeConfig = AssetDatabase.LoadAssetAtPath<UpgradeConfig>(UpgradeConfigPath);

                var projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath);
                if (projectilePrefab != null)
                    controller.projectilePrefab = projectilePrefab.GetComponent<Projectile>();

                go.GetComponent<Rigidbody>().useGravity = false;
                go.AddComponent<BotInputSource>();

                return go;
            });
            UpgradeFarmBotToTank(); // мигрирует FarmBot, оставшийся с задачи 2.1 (там он был просто Health на капсуле)

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

            // Красим независимо от того, только что создан префаб или уже существовал —
            // так перезапуск этого пункта меню перекрашивает и старые серые префабы тоже
            ApplyPrefabMaterial(FarmBotPrefabPath, GetOrCreateMaterial(MatDir + "Mat_FarmBot.mat", FarmBotColor));
            ApplyPrefabMaterial(CratePrefabPath, GetOrCreateMaterial(MatDir + "Mat_Crate.mat", CrateColor));
            ApplyPrefabMaterial(ProjectilePrefabPath, GetOrCreateMaterial(MatDir + "Mat_Projectile.mat", ProjectileColor));

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

        /// <summary>
        /// Задача 2.1 сделала FarmBot просто капсулой с Health. Задача 2.2 достраивает
        /// его до полноценного танка (TankController + BotInputSource) — но идемпотентный
        /// CreatePrefabIfMissing пропустит уже существующий файл, так что для уже
        /// заведённых проектов нужна отдельная миграция содержимого префаба.
        /// </summary>
        static void UpgradeFarmBotToTank()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(FarmBotPrefabPath) is not GameObject existing)
                return; // префаба ещё нет — его уже собрал CreatePrefabIfMissing выше
            if (existing.GetComponent<TankController>() != null)
                return; // уже апгрейжен

            var contents = PrefabUtility.LoadPrefabContents(FarmBotPrefabPath);

            var oldHealth = contents.GetComponent<Health>();
            if (oldHealth != null)
                Object.DestroyImmediate(oldHealth);

            var controller = contents.AddComponent<TankController>(); // Rigidbody и Health вернутся через RequireComponent
            controller.config = AssetDatabase.LoadAssetAtPath<TankConfig>(FarmBotConfigPath);
            controller.upgradeConfig = AssetDatabase.LoadAssetAtPath<UpgradeConfig>(UpgradeConfigPath);

            var projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath);
            if (projectilePrefab != null)
                controller.projectilePrefab = projectilePrefab.GetComponent<Projectile>();

            contents.GetComponent<Rigidbody>().useGravity = false;
            contents.AddComponent<BotInputSource>();

            PrefabUtility.SaveAsPrefabAsset(contents, FarmBotPrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);

            Debug.Log("[Tank Duel] FarmBot обновлён: теперь TankController + BotInputSource (раньше — просто Health).");
        }

        static void ApplyPrefabMaterial(string prefabPath, Material material)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var renderer = prefab != null ? prefab.GetComponent<Renderer>() : null;
            if (renderer == null || renderer.sharedMaterial == material)
                return;

            renderer.sharedMaterial = material;
            EditorUtility.SetDirty(prefab);
        }

        // ---------- Конфиги ----------

        [MenuItem("Tank Duel/Create Default Configs")]
        public static void CreateDefaultConfigs()
        {
            CreateIfMissing<TankConfig>(TankConfigPath, null);

            // Слабее и медленнее игрока: изредка кусается, легко фармится
            CreateIfMissing<TankConfig>(FarmBotConfigPath, config =>
            {
                config.baseHealth = 30f;
                config.baseDamage = 5f;
                config.baseFireRate = 0.4f;  // выстрел раз в ~2.5 сек
                config.baseMoveSpeed = 3f;
            });

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
                        scoreValue = 10, // дороже ящика — бот отстреливается, а не просто стоит
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
                        scoreValue = 5,
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

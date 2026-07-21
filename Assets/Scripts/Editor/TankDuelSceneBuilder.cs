using TankDuel.Core;
using TankDuel.Data;
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
            EnsureComponent<TankBuildSelfCheck>(go); // временный, приёмка 1.1

            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("[Tank Duel] Match Core собран/обновлён: объект «Match» в сцене.");
        }

        [MenuItem("Tank Duel/Create Default Configs")]
        public static void CreateDefaultConfigs()
        {
            CreateIfMissing<TankConfig>("Assets/ScriptableObjects/TankConfig.asset", null);
            CreateIfMissing<UpgradeConfig>("Assets/ScriptableObjects/UpgradeConfig.asset", config =>
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
            {
                Debug.Log($"[Tank Duel] Уже существует, пропущен: {path}");
                return;
            }

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

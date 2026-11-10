using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Archer.EditorTools
{
    /// <summary>
    /// "1. 전체 세팅"에서 호출. RoomLayouts.Chapter1 배치도로 방 프리팹을 만들고 GameConfig.rooms 에 순서대로 넣는다.
    /// 이미 있는 프리팹은 손댄 내용을 지키려고 건너뛴다. 다시 만들려면 그 프리팹을 지우고 전체 세팅 실행.
    /// </summary>
    public static class RoomPrefabs
    {
        const string Dir = "Assets/_Game/Prefabs/Rooms";
        const string ConfigPath = "Assets/_Game/Data/GameConfig.asset";

        public static void Menu()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (cfg == null)
            {
                Debug.LogError("[Archer] GameConfig 가 없어요. 먼저 '1. 전체 세팅'을 실행하세요.");
                return;
            }
            Generate(cfg);
        }

        public static void Generate(GameConfig cfg)
        {
            if (!AssetDatabase.IsValidFolder(Dir))
            {
                Directory.CreateDirectory(Dir);
                AssetDatabase.Refresh();
            }

            var defs = RoomLayouts.Chapter1;
            var list = new List<RoomRoot>();
            int made = 0, kept = 0;
            for (int i = 0; i < defs.Length; i++)
            {
                var path = $"{Dir}/Room_{i + 1:00}_{defs[i].name}.prefab";
                var existing = AssetDatabase.LoadAssetAtPath<RoomRoot>(path);
                if (existing)
                {
                    list.Add(existing);
                    kept++;
                    continue;
                }
                var room = RoomBuilder.Build(cfg, defs[i].layout, defs[i].type, Path.GetFileNameWithoutExtension(path),
                    (prefab, parent) => (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent));
                var saved = PrefabUtility.SaveAsPrefabAsset(room.gameObject, path);
                Object.DestroyImmediate(room.gameObject);
                list.Add(saved.GetComponent<RoomRoot>());
                made++;
            }
            cfg.rooms = list.ToArray();
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Archer] 방 프리팹: 새로 {made}개, 기존 유지 {kept}개 → {Dir}");
        }
    }
}

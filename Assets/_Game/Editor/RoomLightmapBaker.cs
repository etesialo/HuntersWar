using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Archer.EditorTools
{
    /// <summary>
    /// 라이트 굽기가 끝나면, 씬 안의 방(RoomLightmap 컴포넌트가 있는 레벨 프리팹)마다:
    /// 1) 라이트맵 정보를 기록하고
    /// 2) 라이트맵을 Art/Lightmaps/&lt;레벨 이름&gt;/&lt;레벨 이름&gt;_Lightmap-N 으로 복사하고
    /// 3) 레벨 프리팹에 적용(저장)한다.
    /// LightBakeScene 에 레벨 프리팹을 올리고 Generate Lighting 만 누르면 끝.
    /// </summary>
    [InitializeOnLoad]
    public static class RoomLightmapBaker
    {
        const string Root = "Assets/_Game/Art/Lightmaps";

        static RoomLightmapBaker()
        {
            Lightmapping.bakeCompleted += OnBakeCompleted;
        }

        static void OnBakeCompleted()
        {
            // 굽기 직후 에셋 정리가 끝난 다음에 처리
            EditorApplication.delayCall += ProcessAll;
        }

        [MenuItem("Archer/라이트맵을 레벨에 저장 (굽기 후 자동 실행)", false, 21)]
        public static void ProcessAll()
        {
            foreach (var rl in Object.FindObjectsByType<RoomLightmap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Process(rl);
        }

        public static void Process(RoomLightmap rl)
        {
            rl.Capture();
            if (rl.lightmaps == null || rl.lightmaps.Length == 0)
            {
                Debug.LogWarning($"[Archer] {rl.name}: 구운 라이트맵이 없어요 (정적 메시가 굽기에 포함됐는지 확인)", rl);
                return;
            }

            var prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(rl.gameObject);
            var prefabPath = prefabRoot ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabRoot) : null;
            var level = !string.IsNullOrEmpty(prefabPath) ? Path.GetFileNameWithoutExtension(prefabPath) : rl.name;

            // 레벨 폴더로 복사 (레벨 이름으로)
            var dir = $"{Root}/{level}";
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets/_Game/Art", "Lightmaps");
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(Root, level);
            var copies = new Texture2D[rl.lightmaps.Length];
            for (int i = 0; i < rl.lightmaps.Length; i++)
            {
                var src = AssetDatabase.GetAssetPath(rl.lightmaps[i]);
                var dst = $"{dir}/{level}_Lightmap-{i}{Path.GetExtension(src)}";
                if (src == dst) { copies[i] = rl.lightmaps[i]; continue; }
                AssetDatabase.DeleteAsset(dst); // 같은 레벨을 다시 구우면 덮어씀
                AssetDatabase.CopyAsset(src, dst);
                var si = (TextureImporter)AssetImporter.GetAtPath(src);
                var di = (TextureImporter)AssetImporter.GetAtPath(dst);
                EditorUtility.CopySerialized(si, di);
                di.textureType = TextureImporterType.Lightmap;
                di.SaveAndReimport();
                copies[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
            }
            // 이 폴더에 남은 예전 번호 파일 정리 (이번엔 장 수가 줄었을 때)
            foreach (var p in AssetDatabase.FindAssets($"{level}_Lightmap-", new[] { dir }).Select(AssetDatabase.GUIDToAssetPath))
                if (!copies.Any(c => AssetDatabase.GetAssetPath(c) == p)) AssetDatabase.DeleteAsset(p);

            rl.lightmaps = copies;
            rl.directionMaps = new Texture2D[copies.Length];
            EditorUtility.SetDirty(rl);

            // 레벨 프리팹에 적용
            if (prefabRoot)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(rl);
                PrefabUtility.ApplyPrefabInstance(prefabRoot, InteractionMode.AutomatedAction);
                Debug.Log($"[Archer] 라이트맵 저장: {level} → {dir} ({copies.Length}장, 렌더러 {rl.entries.Length}개), 프리팹에 적용", rl);
            }
            else
                Debug.LogWarning($"[Archer] {rl.name} 은 프리팹이 아니라서 라이트맵만 {dir} 에 저장했어요. 프리팹으로 저장해주세요.", rl);
            AssetDatabase.SaveAssets();
        }
    }
}

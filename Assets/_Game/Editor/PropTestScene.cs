using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Archer.EditorTools
{
    /// <summary>
    /// 메뉴 "Archer/3. 프롭 테스트 씬 생성". WAF 환경 프리팹을 폴더(테마)별로 묶어 10열로 진열한다.
    /// 다시 실행하면 씬을 새로 만들어 덮어쓴다.
    /// </summary>
    public static class PropTestScene
    {
        const string ScenePath = "Assets/_Game/Scenes/PropTestScene.unity";
        const string EnvRoot = "Assets/_Game/Art/WAF/01.WorkData/Environment";
        const int Columns = 10;
        const float Gap = 1f;
        const float GroupGap = 4f;

        [MenuItem("Archer/3. 프롭 테스트 씬 생성", false, 2)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.65f);

            // 폴더(테마)별 그룹: Block, Object/<폴더>
            var groups = AssetDatabase.FindAssets("t:Prefab", new[] { EnvRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .GroupBy(GroupName)
                .OrderBy(g => g.Key);

            float z = 0f, maxX = 0f;
            int total = 0;
            foreach (var g in groups)
            {
                var groupRoot = new GameObject(g.Key).transform;
                groupRoot.position = new Vector3(0f, 0f, z);
                Label(groupRoot, font, $"{g.Key} ({g.Count()})", new Vector3(0f, 0f, 1.5f), 0.5f, TextAnchor.LowerLeft);

                float x = 0f, rowZ = 0f, rowDepth = 0f;
                int col = 0;
                foreach (var path in g.OrderBy(p => p))
                {
                    if (col == Columns)
                    {
                        col = 0; x = 0f;
                        rowZ -= rowDepth + Gap + 0.8f;
                        rowDepth = 0f;
                    }
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, groupRoot);
                    var b = LocalBounds(go);
                    // 셀 왼쪽 위 모서리 (x, rowZ) 에 바운드를 맞추고 바닥(y=0)에 올린다
                    go.transform.localPosition = new Vector3(x - b.min.x, -b.min.y, rowZ - b.max.z);
                    Label(groupRoot, font, prefab.name, new Vector3(x + b.size.x * 0.5f, 0f, rowZ - b.size.z - 0.3f), 0.12f, TextAnchor.UpperCenter);

                    x += b.size.x + Gap;
                    rowDepth = Mathf.Max(rowDepth, b.size.z);
                    maxX = Mathf.Max(maxX, x);
                    col++;
                    total++;
                }
                z += rowZ - rowDepth - GroupGap - 2f;
            }

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.25f, 0.27f, 0.3f);
            cam.fieldOfView = 40f;
            cam.farClipPlane = 1000f;
            camGo.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
            // 첫 그룹 쪽을 비춘다 (전체 둘러보기는 씬 뷰에서)
            camGo.transform.position = new Vector3(Mathf.Min(maxX * 0.5f, 12f), 0f, -10f) - camGo.transform.forward * 40f;

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[PropTestScene] {total}개 진열 → {ScenePath}");
        }

        static string GroupName(string path)
        {
            var rel = path.Substring(EnvRoot.Length + 1).Split('/');
            return rel[0] == "Object" && rel.Length > 2 ? rel[1] : rel[0];
        }

        /// <summary>프리팹 루트 기준 바운드 (파티클 제외). 렌더러가 없으면 1x1.</summary>
        static Bounds LocalBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>()
                .Where(r => !(r is ParticleSystemRenderer) && !(r is TrailRenderer)).ToArray();
            if (rs.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            b.center -= go.transform.position;
            return b;
        }

        static void Label(Transform parent, Font font, string text, Vector3 localPos, float size, TextAnchor anchor)
        {
            var go = new GameObject("Label_" + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos + Vector3.up * 0.02f;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = size / (tm.fontSize * 0.1f); // 글자 높이 ≈ size 유닛
            tm.anchor = anchor;
            tm.color = Color.white;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
    }
}

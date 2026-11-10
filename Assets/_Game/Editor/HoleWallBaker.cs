using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Archer.EditorTools
{
    /// <summary>
    /// 구멍 단면(챔퍼 포함)을 라이트 굽기에 넣는다.
    /// 단면은 원래 실행 중에 만들어지는 임시 메시라 라이트매퍼가 못 본다.
    /// 굽기 직전에 저장된 정적 메시(_BakedHoleWalls, 라이트맵 UV 포함)로 만들어 방 안에 넣는다.
    /// 메뉴 "Archer/라이트 굽기 (구멍 단면 포함)" = 준비 + Generate Lighting.
    /// </summary>
    public static class HoleWallBaker
    {
        const string Root = "Assets/_Game/Art/Lightmaps";

        [MenuItem("Archer/라이트 굽기 (구멍 단면 포함)", false, 20)]
        public static void PrepareAndBake()
        {
            if (Lightmapping.isRunning) { Debug.LogWarning("[Archer] 이미 굽는 중이에요."); return; }
            PrepareAll();
            CutFloorHoles();
            Lightmapping.bakeCompleted -= OnBakeDone;
#if UNITY_6000_0_OR_NEWER
            Lightmapping.bakeCancelled -= RestoreFloors;
#endif
            Lightmapping.bakeCompleted += OnBakeDone;
#if UNITY_6000_0_OR_NEWER // 2022 에는 취소 알림이 없음 (취소하면 씬을 다시 열 때 복구)
            Lightmapping.bakeCancelled += RestoreFloors;
#endif
            if (!Lightmapping.BakeAsync()) RestoreFloors();
        }

        static void OnBakeDone()
        {
            Lightmapping.bakeCompleted -= OnBakeDone;
            BakeToVertexColors();
            RestoreFloors();
        }

        // ───────── 구운 빛을 단면 꼭짓점 색으로 옮기기 ─────────
        // 구덩이 안 라이트맵은 노이즈가 심하다. 꼭짓점마다 라이트맵을 넓게 읽고 이웃끼리 한 번 더 평균 내서
        // 꼭짓점 색에 담는다 → 포인트 라이트 색·밝기 변화는 살고 텍셀 얼룩은 사라진다.
        // 그다음 단면은 라이트맵을 끄고(Light Probes) 셰이더의 Vertex GI 로 그린다.

        [MenuItem("Archer/구멍 단면에 구운 빛 다시 담기", false, 23)]
        public static void BakeToVertexColors()
        {
            foreach (var fh in Object.FindObjectsByType<FloorHoles>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var t = fh.transform.Find(FloorHoles.BakedName);
                if (!t || !t.TryGetComponent<MeshRenderer>(out var r) || !t.TryGetComponent<MeshFilter>(out var mf) || !mf.sharedMesh) continue;
                var maps = LightmapSettings.lightmaps;
                if (r.lightmapIndex < 0 || r.lightmapIndex >= maps.Length || !maps[r.lightmapIndex].lightmapColor) continue;

                var mesh = mf.sharedMesh;
                var src = maps[r.lightmapIndex].lightmapColor;
                var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                Graphics.Blit(src, rt);
                var prev = RenderTexture.active; RenderTexture.active = rt;
                var cpu = new Texture2D(src.width, src.height, TextureFormat.RGBAHalf, false, true);
                cpu.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0); cpu.Apply();
                RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);

                // RGBM(sRGB 텍스처) 해독: 색 * 34.49 * a^2.2 (선형). RGBM 이 아니면(알파 1) 그대로
                bool rgbm = src.format.ToString().Contains("RGBA") || src.format.ToString().Contains("ETC2");
                Color Decode(Color c) => rgbm ? (Color)(new Vector4(c.r, c.g, c.b, 0) * (34.49f * Mathf.Pow(c.a, 2.2f))) : c;

                var uv2 = mesh.uv2; var so = r.lightmapScaleOffset; var v = mesh.vertices; var n = mesh.normals;
                var col = new Color[v.Length];
                float px = 1.5f / src.width, py = 1.5f / src.height;
                for (int i = 0; i < v.Length; i++)
                {
                    var p = new Vector2(uv2[i].x * so.x + so.z, uv2[i].y * so.y + so.w);
                    var c = Color.clear;
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                            c += Decode(cpu.GetPixelBilinear(p.x + dx * px, p.y + dy * py));
                    col[i] = c / 9f;
                }
                Object.DestroyImmediate(cpu);

                // 이웃 꼭짓점끼리 평균 (반경 0.35m, 같은 쪽을 보는 면만) 2번
                for (int pass = 0; pass < 2; pass++)
                {
                    var next = new Color[v.Length];
                    for (int i = 0; i < v.Length; i++)
                    {
                        Color sum = Color.clear; float w = 0f;
                        for (int j = 0; j < v.Length; j++)
                        {
                            float d = (v[j] - v[i]).sqrMagnitude;
                            if (d > 0.35f * 0.35f || Vector3.Dot(n[i], n[j]) < 0.2f) continue;
                            float k = 1f - Mathf.Sqrt(d) / 0.35f;
                            sum += col[j] * k; w += k;
                        }
                        next[i] = w > 0f ? sum / w : col[i];
                    }
                    col = next;
                }
                for (int i = 0; i < col.Length; i++) col[i].a = 1f;
                mesh.colors = col;
                EditorUtility.SetDirty(mesh);

                Undo.RecordObject(r, "구운 빛 꼭짓점 색");
                r.receiveGI = ReceiveGI.LightProbes;
                r.lightmapIndex = -1;
                if (r.sharedMaterial && r.sharedMaterial.HasProperty("_VertexGI"))
                {
                    r.sharedMaterial.SetFloat("_VertexGI", 1f);
                    r.sharedMaterial.EnableKeyword("_VERTEX_GI");
                    EditorUtility.SetDirty(r.sharedMaterial);
                }
                Debug.Log($"[Archer] 구멍 단면에 구운 빛 담음: {fh.transform.parent.name} (꼭짓점 {v.Length}개)", r);
            }
            AssetDatabase.SaveAssets();
        }

        // ───────── 굽는 동안만 바닥에 구멍 뚫기 ─────────
        // 바닥 셰이더는 화면에서만 구멍을 잘라낸다. 라이트매퍼는 바닥을 통째로 봐서 구멍이 뚜껑으로 덮인 채 구워진다.
        // 굽는 동안 바닥 메시에서 구멍 칸 면을 뺀 임시 메시를 쓰고, 끝나면(또는 취소되면) 원래 메시로 되돌린다.
        // 라이트맵 UV(UV2)는 그대로라 되돌려도 구운 결과가 맞는다.

        const string TempPrefix = "__BakeFloor_";

        static void CutFloorHoles()
        {
            foreach (var fh in Object.FindObjectsByType<FloorHoles>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var cells = fh.Cells();
                var room = fh.GetComponentInParent<RoomRoot>();
                var floor = room ? room.transform.Find("Floor") : null;
                if (cells.Count == 0 || !floor) continue;
                foreach (var mf in floor.GetComponentsInChildren<MeshFilter>())
                {
                    var src = mf.sharedMesh;
                    if (!src || src.name.StartsWith(TempPrefix)) continue;
                    var v = src.vertices; var tris = src.triangles;
                    var keep = new List<int>(tris.Length);
                    int removed = 0;
                    for (int i = 0; i < tris.Length; i += 3)
                    {
                        var c = (v[tris[i]] + v[tris[i + 1]] + v[tris[i + 2]]) / 3f;
                        var local = room.transform.InverseTransformPoint(mf.transform.TransformPoint(c));
                        if (cells.Contains(RoomGrid.ToCell(local))) { removed++; continue; }
                        keep.Add(tris[i]); keep.Add(tris[i + 1]); keep.Add(tris[i + 2]);
                    }
                    if (removed == 0) continue;
                    var cut = Object.Instantiate(src);
                    cut.name = TempPrefix + src.name;
                    cut.hideFlags = HideFlags.DontSave;
                    cut.subMeshCount = 1;
                    cut.SetTriangles(keep, 0);
                    if (!mf.gameObject.TryGetComponent<BakeFloorBackup>(out var backup)) backup = mf.gameObject.AddComponent<BakeFloorBackup>();
                    backup.hideFlags = HideFlags.HideInInspector;
                    backup.original = src;
                    mf.sharedMesh = cut;
                    if (mf.TryGetComponent<MeshRenderer>(out var mr))
                    {
                        backup.cast = mr.shadowCastingMode;
                        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    }
                    Debug.Log($"[Archer] 굽기용 바닥 구멍: {room.name} 면 {removed}개 제외", mf);
                }
            }
        }

        [InitializeOnLoadMethod]
        static void RestoreOnLoad()
        {
            // 굽는 중에 에디터가 꺼졌어도 다음에 켤 때 원래 바닥으로 되돌린다
            EditorApplication.delayCall += RestoreFloors;
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += (s, m) => RestoreFloors();
        }

        static void RestoreFloors()
        {
            Lightmapping.bakeCompleted -= RestoreFloors;
#if UNITY_6000_0_OR_NEWER
            Lightmapping.bakeCancelled -= RestoreFloors;
#endif
            foreach (var b in Object.FindObjectsByType<BakeFloorBackup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (b.TryGetComponent<MeshFilter>(out var mf) && b.original)
                {
                    var temp = mf.sharedMesh;
                    mf.sharedMesh = b.original;
                    if (temp && temp.name.StartsWith(TempPrefix)) Object.DestroyImmediate(temp);
                }
                if (b.TryGetComponent<MeshRenderer>(out var mr)) mr.shadowCastingMode = b.cast;
                Object.DestroyImmediate(b);
            }
        }

        [MenuItem("Archer/구멍 단면만 굽기 준비", false, 22)]
        public static void PrepareAll()
        {
            foreach (var fh in Object.FindObjectsByType<FloorHoles>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                Prepare(fh);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 단면은 '둘레 거리(UV0.x, m) × 아래→위 거리(UV0.y, m)'로 정확히 펴지는 띠다.
        /// 그대로 쓰되, 긴 띠를 정사각형에 가깝게 여러 줄로 잘라 쌓는다. 줄이 바뀌는 곳만 꼭짓점을 나눈다.
        /// 가로·세로 같은 비율로 0~1 에 맞춰서 늘어남이 없다.
        /// </summary>
        static Mesh LayoutLightmapUV(Mesh src)
        {
            var v = src.vertices; var n = src.normals; var uv0 = src.uv;
            var uv3 = new List<Vector4>(); src.GetUVs(3, uv3);
            var tris = src.triangles;

            float minU = float.MaxValue, maxU = float.MinValue, maxV = 0f;
            foreach (var t in uv0) { minU = Mathf.Min(minU, t.x); maxU = Mathf.Max(maxU, t.x); maxV = Mathf.Max(maxV, t.y); }
            const float pad = 0.15f;                      // 줄 사이 간격 (m)
            float rowH = maxV + pad;
            float totalLen = maxU - minU;
            int rows = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(totalLen / rowH)));
            float rowW = totalLen / rows;

            // 삼각형마다 줄을 정한다 (왼쪽 가장자리 기준), 꼭짓점은 (원래 번호, 줄)로 나눔
            var map = new Dictionary<long, int>();
            var nv = new List<Vector3>(); var nn = new List<Vector3>(); var n0 = new List<Vector2>();
            var n2 = new List<Vector2>(); var n3 = new List<Vector4>(); var nt = new List<int>(tris.Length);
            var rowStart = new float[rows];
            for (int r = 0; r < rows; r++) rowStart[r] = minU + r * rowW;
            float maxRowExtent = 0f;
            for (int i = 0; i < tris.Length; i += 3)
            {
                float left = Mathf.Min(uv0[tris[i]].x, Mathf.Min(uv0[tris[i + 1]].x, uv0[tris[i + 2]].x));
                int row = Mathf.Clamp(Mathf.FloorToInt((left - minU) / rowW + 1e-4f), 0, rows - 1);
                for (int k = 0; k < 3; k++)
                {
                    int o = tris[i + k];
                    long key = ((long)o << 8) | (long)row;
                    if (!map.TryGetValue(key, out int idx))
                    {
                        idx = nv.Count; map[key] = idx;
                        nv.Add(v[o]); nn.Add(n[o]); n0.Add(uv0[o]);
                        if (uv3.Count == v.Length) n3.Add(uv3[o]);
                        float x = uv0[o].x - rowStart[row];
                        n2.Add(new Vector2(x, uv0[o].y + row * rowH));
                        maxRowExtent = Mathf.Max(maxRowExtent, x);
                    }
                    nt.Add(idx);
                }
            }
            // 같은 비율로 0~1 에 맞춤 (약간 여백)
            float size = Mathf.Max(maxRowExtent, rows * rowH) * 1.02f;
            for (int i = 0; i < n2.Count; i++) n2[i] = n2[i] / size + new Vector2(0.01f, 0.01f);

            var m = new Mesh { name = src.name, indexFormat = nv.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            m.SetVertices(nv); m.SetNormals(nn); m.SetUVs(0, n0); m.SetUVs(1, n2);
            if (n3.Count == nv.Count) m.SetUVs(3, n3);
            m.SetTriangles(nt, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            Object.DestroyImmediate(src);
            return m;
        }

        public static void Prepare(FloorHoles fh)
        {
            var old = fh.transform.Find(FloorHoles.BakedName);
            var mesh = fh.BuildWallMesh();
            if (mesh == null)
            {
                if (old) Undo.DestroyObjectImmediate(old.gameObject);
                return;
            }

            // 라이트맵 UV(UV2)를 직접 편다 (자동 생성은 긴 띠를 잘게 쪼개고 늘려서 얼룩짐)
            mesh = LayoutLightmapUV(mesh);

            // 레벨 이름으로 메시 저장
            var room = fh.GetComponentInParent<RoomRoot>();
            var prefabRoot = room ? PrefabUtility.GetOutermostPrefabInstanceRoot(room.gameObject) : null;
            var prefabPath = prefabRoot ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabRoot) : null;
            var level = !string.IsNullOrEmpty(prefabPath) ? Path.GetFileNameWithoutExtension(prefabPath) : (room ? room.name : fh.name);
            var dir = $"{Root}/{level}";
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets/_Game/Art", "Lightmaps");
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(Root, level);
            var path = $"{dir}/{level}_HoleWalls.asset";
            mesh.name = $"{level}_HoleWalls";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                mesh = existing;
                EditorUtility.SetDirty(mesh);
            }
            else AssetDatabase.CreateAsset(mesh, path);

            // 방 안의 정적 오브젝트
            GameObject go;
            if (old) go = old.gameObject;
            else
            {
                go = new GameObject(FloorHoles.BakedName);
                Undo.RegisterCreatedObjectUndo(go, "구멍 단면 굽기 준비");
                go.transform.SetParent(fh.transform, false);
            }
            go.transform.localPosition = new Vector3(0f, -fh.depth, 0f);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            // (유니티 오브젝트는 ?? 를 쓰면 '가짜 null' 때문에 추가가 안 될 수 있어서 직접 확인)
            if (!go.TryGetComponent<MeshFilter>(out var mf)) mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            if (!go.TryGetComponent<MeshRenderer>(out var mr)) mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = fh.wallMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            mr.receiveShadows = true;
            // 굽는 동안은 라이트맵을 받게 둔다 → 굽기가 끝나면 그 값을 꼭짓점 색으로 옮기고 라이트맵은 끈다
            mr.receiveGI = ReceiveGI.Lightmaps;
            mr.scaleInLightmap = 1f;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

            Undo.RecordObject(fh, "구멍 단면 굽기 준비");
            fh.bakedHash = fh.ShapeHash();
            EditorUtility.SetDirty(fh);
            fh.Rebuild(); // 실행 중 단면을 지우고 저장된 단면을 쓰게
            Debug.Log($"[Archer] 구멍 단면 굽기 준비: {level} → {path}", go);
        }
    }
}

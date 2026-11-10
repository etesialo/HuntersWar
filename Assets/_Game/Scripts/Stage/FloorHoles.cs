using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Archer
{
    /// <summary>
    /// 바닥 구멍. 같은 오브젝트의 GridArea 칸 목록(판정은 GridArea 가 합쳐서 만듦)의 모양대로
    /// - 구멍 마스크 텍스처(전역 _RoomHoleMask) → 바닥 셰이더(Archer/StylizedEnv, Floor Holes 켬)가 그 부분을 잘라낸다
    /// - 둘레 단면 벽 (코너는 둥근 챔퍼)
    /// - (선택) 아래 물 판. 방에 직접 넣은 Water 판이 있으면 끄면 된다
    /// 을 만든다. 만든 메시/텍스처는 저장하지 않고 켜질 때마다 다시 만든다(프리팹엔 칸 정보만 남음).
    /// </summary>
    [ExecuteAlways]
    public class FloorHoles : MonoBehaviour
    {
        public Material wallMaterial;
        public Material waterMaterial;
        public Material maskMaterial; // (예전 깊이 마스크용, 안 씀)
        [Tooltip("구멍 아래 물 판을 자동으로 만들지 (방에 직접 넣은 Water 판이 있으면 끔)")]
        public bool generateWater = false;
        [Tooltip("마스크 해상도: 한 칸당 픽셀")] [Range(4, 32)] public int maskPixelsPerCell = 16;

        static readonly int MaskId = Shader.PropertyToID("_RoomHoleMask");
        static readonly int RectId = Shader.PropertyToID("_RoomHoleRect");
        Texture2D maskTex;
        int maskHash;
        bool maskUploaded;
        bool rebuilding;
        [Tooltip("단면 깊이 (m)")] public float depth = 0.7f;
        [Tooltip("물 높이 (바닥 아래, m)")] public float waterDepth = 0.4f;
        [Tooltip("코너 챔퍼 반경 (m, 최대 0.37)")] [Range(0f, 0.37f)] public float cornerRadius = 0.2f;
        [Range(1, 8)] public int cornerSegments = 4;
        [Tooltip("바닥과 절벽이 만나는 윗 모서리를 둥글게 깎는 반경 (m)")] [Range(0f, 0.4f)] public float edgeRadius = 0.14f;
        [Range(1, 8)] public int edgeSegments = 4;

        const string GenPrefix = "__Hole";
        /// <summary>라이트 굽기용으로 저장된 단면 오브젝트 이름 (프리팹에 남음, 정적)</summary>
        public const string BakedName = "_BakedHoleWalls";
        [HideInInspector] public int bakedHash; // 저장된 단면이 만들어질 때의 구멍 모양
        readonly List<Object> generated = new();

        /// <summary>구멍이 없는 방에서도 바닥이 잘리지 않게 기본값(검정 = 구멍 없음)</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        static void ResetGlobalMask()
        {
            Shader.SetGlobalTexture(MaskId, Texture2D.blackTexture);
            Shader.SetGlobalVector(RectId, new Vector4(-RoomGrid.Width * 0.5f, -RoomGrid.Length * 0.5f, 1f / RoomGrid.Width, 1f / RoomGrid.Length));
        }

        void OnEnable() => Rebuild();
        void OnDisable() { Clear(); ClearMask(); }
        void OnTransformChildrenChanged() { if (!rebuilding && isActiveAndEnabled) Rebuild(); }
#if UNITY_EDITOR
        void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () => { if (this) Rebuild(); };
        }
#endif

        /// <summary>구멍 칸: 같은 오브젝트의 GridArea 칸 목록 (+ 예전 방식의 칸마다 자식 "Hole")</summary>
        public HashSet<Vector2Int> Cells()
        {
            var set = new HashSet<Vector2Int>();
            if (TryGetComponent<GridArea>(out var area)) set.UnionWith(area.cells);
            foreach (Transform c in transform)
                if (!c.name.StartsWith("_")) // __Hole* (실행 중 생성), _BakedHoleWalls (굽기용), _Col (판정) 제외
                    set.Add(RoomGrid.ToCell(c.localPosition));
            return set;
        }

        /// <summary>구멍 모양 + 단면 설정으로 만든 값 (저장된 단면이 지금 모양과 같은지 비교용)</summary>
        public int ShapeHash()
        {
            unchecked
            {
                int h = 17;
                var list = new List<Vector2Int>(Cells());
                list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
                foreach (var c in list) h = h * 31 + c.x * 1000 + c.y;
                h = h * 31 + Mathf.RoundToInt(depth * 1000) + Mathf.RoundToInt(cornerRadius * 1000) * 7
                    + Mathf.RoundToInt(edgeRadius * 1000) * 13 + cornerSegments * 17 + edgeSegments * 19;
                return h;
            }
        }

        /// <summary>저장된(구운) 단면 오브젝트. 지금 구멍 모양과 같을 때만 유효</summary>
        public MeshRenderer BakedWalls()
        {
            var t = transform.Find(BakedName);
            if (!t) return null;
            var r = t.GetComponent<MeshRenderer>();
            bool valid = r && bakedHash == ShapeHash() && t.GetComponent<MeshFilter>() && t.GetComponent<MeshFilter>().sharedMesh;
            if (r) r.enabled = valid; // 모양이 바뀌었으면 숨기고 실행 중 단면을 쓴다
            return valid ? r : null;
        }

        /// <summary>굽기용 단면 메시 (실행 중 단면과 같은 모양)</summary>
        public Mesh BuildWallMesh()
        {
            var cells = Cells();
            if (cells.Count == 0) return null;
            var mask = new Mesh { name = "HoleMaskUnused" };
            var walls = new Mesh { name = "HoleWalls" };
            BuildMeshes(TraceLoops(cells), mask, walls);
            DestroyNow(mask);
            return walls;
        }

        public void Rebuild()
        {
            if (rebuilding) return;
            if (!gameObject.scene.IsValid()) return; // 프리팹 원본 에셋(씬 밖)에서는 만들지 않음
            rebuilding = true;
            try
            {
                Clear();
                var cells = Cells();
                var loops = cells.Count > 0 ? TraceLoops(cells) : new List<List<Vector2>>();
                var rounded = new List<List<Vector2>>();
                // 바닥은 둥근 턱이 시작되는 곳(외곽선에서 edgeRadius 만큼 바깥)까지 잘라낸다
                foreach (var l in loops) rounded.Add(Offset(Round(l), Mathf.Clamp(edgeRadius, 0f, depth * 0.9f)));
                BuildMask(rounded);
                BakedWalls(); // 구멍이 모두 지워졌으면 구워둔 단면도 숨김
                if (cells.Count == 0) return;

                // 구워둔 단면이 지금 모양과 같으면 그걸 쓰고(라이트맵 적용됨), 아니면 실행 중에 만든다
                if (BakedWalls() == null)
                {
                    var mask = new Mesh { name = "HoleMaskUnused" };
                    var walls = new Mesh { name = "HoleWalls" };
                    BuildMeshes(loops, mask, walls);
                    DestroyNow(mask);
                    Make("Walls", walls, wallMaterial, new Vector3(0f, -depth, 0f), true, true); // 물에 그림자 띠
                }
                if (generateWater)
                {
                    var water = new Mesh { name = "HoleWater" };
                    BuildWater(cells, water);
                    Make("Water", water, waterMaterial, new Vector3(0f, -waterDepth, 0f), true, false);
                }
            }
            finally { rebuilding = false; }
        }

        static void DestroyNow(Object o)
        {
            if (!o) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        /// <summary>
        /// 둥근 외곽선 안쪽을 1로 칠한 마스크 텍스처를 만들고 전역으로 넘긴다 (방 전체 18 x 28칸 범위).
        /// 바닥 셰이더는 월드 xz 로 이 텍스처를 읽어 구멍 부분을 잘라낸다.
        /// </summary>
        void BuildMask(List<List<Vector2>> loops)
        {
            int w = RoomGrid.Cols * maskPixelsPerCell, h = RoomGrid.Rows * maskPixelsPerCell;
            if (maskTex == null || maskTex.width != w || maskTex.height != h)
            {
                ClearMask();
                maskUploaded = false;
                maskTex = new Texture2D(w, h, TextureFormat.R8, false, true)
                {
                    name = "RoomHoleMask", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave
                };
            }
            var px = new byte[w * h];
            if (loops.Count > 0)
            {
                float minX = -RoomGrid.Width * 0.5f, minZ = -RoomGrid.Length * 0.5f;
                float step = RoomGrid.Cell / maskPixelsPerCell;
                // 각 줄에서 외곽선과 만나는 x 를 구해 짝수-홀수 규칙으로 칠함 (섬도 처리됨)
                var xs = new List<float>();
                for (int y = 0; y < h; y++)
                {
                    float z = minZ + (y + 0.5f) * step;
                    xs.Clear();
                    foreach (var loop in loops)
                        for (int i = 0; i < loop.Count; i++)
                        {
                            var a = loop[i]; var b = loop[(i + 1) % loop.Count];
                            if ((a.y > z) == (b.y > z)) continue;
                            xs.Add(a.x + (z - a.y) / (b.y - a.y) * (b.x - a.x));
                        }
                    xs.Sort();
                    for (int k = 0; k + 1 < xs.Count; k += 2)
                    {
                        int x0 = Mathf.Clamp(Mathf.CeilToInt((xs[k] - minX) / step - 0.5f), 0, w);
                        int x1 = Mathf.Clamp(Mathf.FloorToInt((xs[k + 1] - minX) / step - 0.5f), -1, w - 1);
                        for (int x = x0; x <= x1; x++) px[y * w + x] = 255;
                    }
                }
            }
            // 모양이 그대로면 GPU 로 다시 올리지 않는다 (잦은 업로드가 일부 드라이버에서 크래시를 냄)
            int hash = 17;
            unchecked { for (int i = 0; i < px.Length; i += 7) hash = hash * 31 + px[i]; hash = hash * 31 + px.Length; }
            if (hash != maskHash || !maskUploaded)
            {
                maskTex.SetPixelData(px, 0);
                maskTex.Apply(false);
                maskHash = hash;
                maskUploaded = true;
            }
            // 방 원점 기준 (방은 원점에 놓인다). rect = (minX, minZ, 1/width, 1/length)
            var o = transform.parent ? transform.parent.position : Vector3.zero;
            Shader.SetGlobalTexture(MaskId, maskTex);
            Shader.SetGlobalVector(RectId, new Vector4(o.x - RoomGrid.Width * 0.5f, o.z - RoomGrid.Length * 0.5f,
                1f / RoomGrid.Width, 1f / RoomGrid.Length));
        }

        void ClearMask()
        {
            if (maskTex)
            {
                if (Shader.GetGlobalTexture(MaskId) == maskTex) Shader.SetGlobalTexture(MaskId, Texture2D.blackTexture);
                DestroyNow(maskTex);
                maskTex = null;
            }
        }

        void Make(string n, Mesh mesh, Material mat, Vector3 pos, bool receive, bool cast)
        {
            var go = new GameObject(GenPrefix + n);
            go.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = cast ? ShadowCastingMode.TwoSided : ShadowCastingMode.Off;
            r.receiveShadows = receive;
            generated.Add(go);
            generated.Add(mesh);
        }

        void Clear()
        {
            // 지우는 동안 자식 변경 콜백이 다시 Rebuild 를 부르지 않게
            bool was = rebuilding;
            rebuilding = true;
            try
            {
                var list = generated.ToArray();
                generated.Clear();
                foreach (var o in list)
                    if (o && !(o is GameObject)) DestroyNow(o); // 메시 (오브젝트는 아래에서 이름으로 지움)
                for (int i = transform.childCount - 1; i >= 0; i--)
                {
                    var c = transform.GetChild(i);
                    if (c.name.StartsWith(GenPrefix)) DestroyNow(c.gameObject);
                }
            }
            finally { rebuilding = was; }
        }

        // ───────── 외곽선 ─────────

        /// <summary>구멍 칸들의 경계를 따라 닫힌 고리들(구멍이 왼쪽, 위에서 볼 때 반시계)을 만든다. 좌표는 x,z.</summary>
        static List<List<Vector2>> TraceLoops(HashSet<Vector2Int> cells)
        {
            // 격자점 정수 좌표: 칸(c, r)의 왼쪽-아래 모서리 = (c, rows - r - 1) (z 가 위로 증가하게)
            var next = new Dictionary<Vector2Int, List<Vector2Int>>();
            void Edge(Vector2Int a, Vector2Int b)
            {
                if (!next.TryGetValue(a, out var l)) next[a] = l = new List<Vector2Int>();
                l.Add(b);
            }
            foreach (var c in cells)
            {
                int x0 = c.x, z0 = RoomGrid.Rows - 1 - c.y, x1 = x0 + 1, z1 = z0 + 1;
                if (!cells.Contains(new Vector2Int(c.x, c.y + 1))) Edge(new(x0, z0), new(x1, z0)); // 아래
                if (!cells.Contains(new Vector2Int(c.x + 1, c.y))) Edge(new(x1, z0), new(x1, z1)); // 오른쪽
                if (!cells.Contains(new Vector2Int(c.x, c.y - 1))) Edge(new(x1, z1), new(x0, z1)); // 위
                if (!cells.Contains(new Vector2Int(c.x - 1, c.y))) Edge(new(x0, z1), new(x0, z0)); // 왼쪽
            }

            var loops = new List<List<Vector2>>();
            while (true)
            {
                Vector2Int start = default;
                bool found = false;
                foreach (var kv in next) if (kv.Value.Count > 0) { start = kv.Key; found = true; break; }
                if (!found) break;

                var pts = new List<Vector2Int> { start };
                var cur = start;
                Vector2Int prevDir = Vector2Int.zero;
                for (int guard = 0; guard < 10000; guard++)
                {
                    var outs = next[cur];
                    int pick = 0;
                    if (outs.Count > 1 && prevDir != Vector2Int.zero)
                    {
                        // 대각선으로 맞닿은 곳: 왼쪽으로 꺾는 쪽을 골라 고리를 분리
                        var left = new Vector2Int(-prevDir.y, prevDir.x);
                        for (int i = 0; i < outs.Count; i++) if (outs[i] - cur == left) pick = i;
                    }
                    var nxt = outs[pick];
                    outs.RemoveAt(pick);
                    prevDir = nxt - cur;
                    cur = nxt;
                    if (cur == start) break;
                    pts.Add(cur);
                }

                // 직선 위의 점 제거 → 꺾이는 모서리만
                var corners = new List<Vector2>();
                for (int i = 0; i < pts.Count; i++)
                {
                    var a = pts[(i - 1 + pts.Count) % pts.Count];
                    var b = pts[i];
                    var c = pts[(i + 1) % pts.Count];
                    if ((b - a) != (c - b)) corners.Add(ToWorld(b));
                }
                if (corners.Count >= 3) loops.Add(corners);
            }
            return loops;
        }

        static Vector2 ToWorld(Vector2Int g) => new(
            (g.x - RoomGrid.Cols * 0.5f) * RoomGrid.Cell,
            (g.y - RoomGrid.Rows * 0.5f) * RoomGrid.Cell);

        /// <summary>모서리마다 둥근 챔퍼(2차 베지어)를 넣은 고리</summary>
        List<Vector2> Round(List<Vector2> corners)
        {
            if (cornerRadius <= 0.001f) return corners;
            var outPts = new List<Vector2>();
            int n = corners.Count;
            for (int i = 0; i < n; i++)
            {
                var p = corners[i];
                var a = corners[(i - 1 + n) % n];
                var b = corners[(i + 1) % n];
                float r = Mathf.Min(cornerRadius, Vector2.Distance(a, p) * 0.5f, Vector2.Distance(p, b) * 0.5f);
                var s = p + (a - p).normalized * r;
                var e = p + (b - p).normalized * r;
                for (int k = 0; k <= cornerSegments; k++)
                {
                    float t = k / (float)cornerSegments;
                    outPts.Add((1 - t) * (1 - t) * s + 2 * (1 - t) * t * p + t * t * e);
                }
            }
            return outPts;
        }

        /// <summary>고리의 긴 변을 maxLen 이하 조각으로 나눈다 (모양은 그대로)</summary>
        static List<Vector2> Subdivide(List<Vector2> loop, float maxLen)
        {
            var o = new List<Vector2>(loop.Count * 2);
            for (int i = 0; i < loop.Count; i++)
            {
                var a = loop[i]; var b = loop[(i + 1) % loop.Count];
                o.Add(a);
                int n = Mathf.FloorToInt(Vector2.Distance(a, b) / maxLen);
                for (int k = 1; k <= n; k++)
                {
                    float t = k / (float)(n + 1);
                    o.Add(Vector2.Lerp(a, b, t));
                }
            }
            return o;
        }

        /// <summary>고리를 바깥(구멍 반대쪽)으로 dist 만큼 넓힌다</summary>
        static List<Vector2> Offset(List<Vector2> loop, float dist)
        {
            if (dist <= 0.0001f) return loop;
            int n = loop.Count;
            var o = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                var p = loop[i];
                var d = ((p - loop[(i - 1 + n) % n]).normalized + (loop[(i + 1) % n] - p).normalized).normalized;
                var outward = new Vector2(d.y, -d.x); // 진행 방향 오른쪽 = 구멍 바깥
                o.Add(p + outward * dist);
            }
            return o;
        }

        // ───────── 메시 ─────────

        void BuildMeshes(List<List<Vector2>> loops, Mesh mask, Mesh walls)
        {
            var mv = new List<Vector3>();
            var mt = new List<int>();
            var wv = new List<Vector3>();
            var wn = new List<Vector3>();
            var wuv = new List<Vector2>();
            var wt = new List<int>();
            // UV0.x = 둘레를 따라간 거리(m). 고리가 여러 개면 이어서 세고 사이에 1m 간격 (라이트맵 UV 배치에 씀)
            float uStart = 0f;

            foreach (var raw in loops)
            {
                // 곧은 변도 0.375m(반 칸)마다 꼭짓점을 넣는다 → 꼭짓점 색으로 구운 빛을 담을 때 변화가 살아남
                var loop = Subdivide(Round(raw), RoomGrid.Cell * 0.5f);
                int n = loop.Count;

                // 마스크: 반시계 고리 = 구멍, 시계 고리 = 구멍 안의 바닥 섬(지원 안 함 → 건너뜀)
                if (SignedArea(loop) > 0f)
                {
                    int baseIdx = mv.Count;
                    foreach (var p in loop) mv.Add(new Vector3(p.x, 0f, p.y));
                    foreach (int idx in EarClip(loop)) mt.Add(baseIdx + idx);
                }

                // 단면 벽: 아래(0)에서 수직으로 올라가다가, 윗부분은 둥글게 바깥(바닥 쪽)으로 휘어
                // 바닥 높이(depth)에서 바닥과 수평으로 만난다. 면은 구멍 안쪽(진행 방향 왼쪽)을 본다.
                float lip = Mathf.Clamp(edgeRadius, 0f, depth * 0.9f);
                int arc = lip > 0.001f ? edgeSegments : 0;
                int ringCount = 2 + arc; // 바닥, 둥근 면 시작, 둥근 면 단계들
                int wb = wv.Count;
                float u = uStart;
                for (int i = 0; i <= n; i++)
                {
                    var p = loop[i % n];
                    var prev = loop[(i - 1 + n) % n];
                    var nx = loop[(i + 1) % n];
                    var d = ((p - prev).normalized + (nx - p).normalized).normalized;
                    var left = new Vector3(-d.y, 0f, d.x);   // 구멍 안쪽
                    var outward = -left;                      // 바닥 쪽
                    if (i > 0) u += Vector2.Distance(prev, p);
                    var basePt = new Vector3(p.x, 0f, p.y);
                    // 수직 구간
                    wv.Add(basePt); wn.Add(left); wuv.Add(new Vector2(u, 0f));
                    wv.Add(basePt + Vector3.up * (depth - lip)); wn.Add(left); wuv.Add(new Vector2(u, depth - lip));
                    // 둥근 턱: 중심 = 바깥쪽 lip, 높이 depth - lip
                    var center = basePt + outward * lip + Vector3.up * (depth - lip);
                    for (int k = 1; k <= arc; k++)
                    {
                        float th = k / (float)arc * Mathf.PI * 0.5f;
                        float cs = Mathf.Cos(th), sn = Mathf.Sin(th);
                        wv.Add(center - outward * (lip * cs) + Vector3.up * (lip * sn));
                        wn.Add((left * cs + Vector3.up * sn).normalized);
                        wuv.Add(new Vector2(u, depth - lip + lip * th));
                    }
                }
                for (int i = 0; i < n; i++)
                for (int k = 0; k < ringCount - 1; k++)
                {
                    int a0 = wb + i * ringCount + k, a1 = a0 + 1;
                    int b0 = wb + (i + 1) * ringCount + k, b1 = b0 + 1;
                    // 왼쪽을 향하도록 감는다 (유니티는 시계 방향이 앞면)
                    wt.Add(a0); wt.Add(a1); wt.Add(b0);
                    wt.Add(b0); wt.Add(a1); wt.Add(b1);
                }
                uStart = u + 1f;
            }

            mask.SetVertices(mv);
            mask.SetTriangles(mt, 0);
            mask.RecalculateBounds();

            walls.SetVertices(wv);
            walls.SetNormals(wn);
            walls.SetUVs(0, wuv);
            // UV3 = (부드러운 노멀, 정점 AO). 비워두면 셰이더가 쓰레기 값을 읽어 새까매질 수 있어서 채워둔다
            var uv3 = new List<Vector4>(wn.Count);
            foreach (var nrm in wn) uv3.Add(new Vector4(nrm.x, nrm.y, nrm.z, 0f));
            walls.SetUVs(3, uv3);
            walls.SetTriangles(wt, 0);
            walls.RecalculateBounds();
            if (walls.vertexCount > 0) FixWinding(walls);
        }

        /// <summary>삼각형 앞면이 정점 노멀(구멍 안쪽)과 반대면 뒤집는다</summary>
        static void FixWinding(Mesh m)
        {
            var v = m.vertices; var nrm = m.normals; var t = m.triangles;
            var tri0 = Vector3.Cross(v[t[1]] - v[t[0]], v[t[2]] - v[t[0]]);
            if (Vector3.Dot(tri0, nrm[t[0]]) >= 0f) return;
            for (int i = 0; i < t.Length; i += 3) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
            m.triangles = t;
        }

        void BuildWater(HashSet<Vector2Int> cells, Mesh m)
        {
            // 구멍 칸들을 감싸는 사각형 하나 (바닥에 가려져 구멍으로만 보인다)
            int minC = int.MaxValue, maxC = int.MinValue, minR = int.MaxValue, maxR = int.MinValue;
            foreach (var c in cells)
            {
                minC = Mathf.Min(minC, c.x); maxC = Mathf.Max(maxC, c.x);
                minR = Mathf.Min(minR, c.y); maxR = Mathf.Max(maxR, c.y);
            }
            var a = RoomGrid.Center(minC, maxR); var b = RoomGrid.Center(maxC, minR);
            float h = RoomGrid.Cell;
            float x0 = a.x - h, x1 = b.x + h, z0 = a.z - h, z1 = b.z + h;
            m.SetVertices(new List<Vector3> { new(x0, 0, z0), new(x0, 0, z1), new(x1, 0, z1), new(x1, 0, z0) });
            m.SetNormals(new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            m.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            m.RecalculateBounds();
        }

        static float SignedArea(List<Vector2> p)
        {
            float a = 0f;
            for (int i = 0; i < p.Count; i++)
            {
                var q = p[i]; var r = p[(i + 1) % p.Count];
                a += q.x * r.y - r.x * q.y;
            }
            return a * 0.5f;
        }

        /// <summary>반시계 단순 다각형 귀 자르기. 위(+y)에서 보이는 면으로 감는다.</summary>
        static List<int> EarClip(List<Vector2> p)
        {
            var result = new List<int>();
            var idx = new List<int>();
            for (int i = 0; i < p.Count; i++) idx.Add(i);
            int guard = 0;
            while (idx.Count > 3 && guard++ < 20000)
            {
                bool cut = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int i0 = idx[(i - 1 + idx.Count) % idx.Count], i1 = idx[i], i2 = idx[(i + 1) % idx.Count];
                    var a = p[i0]; var b = p[i1]; var c = p[i2];
                    if (Cross(b - a, c - b) <= 1e-6f) continue; // 오목
                    bool inside = false;
                    foreach (int j in idx)
                    {
                        if (j == i0 || j == i1 || j == i2) continue;
                        if (InTri(p[j], a, b, c)) { inside = true; break; }
                    }
                    if (inside) continue;
                    // 위에서 보이게: (a, c, b) 순서
                    result.Add(i0); result.Add(i2); result.Add(i1);
                    idx.RemoveAt(i);
                    cut = true;
                    break;
                }
                if (!cut) break;
            }
            if (idx.Count == 3) { result.Add(idx[0]); result.Add(idx[2]); result.Add(idx[1]); }
            return result;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
            return d1 > 0 && d2 > 0 && d3 > 0;
        }
    }
}

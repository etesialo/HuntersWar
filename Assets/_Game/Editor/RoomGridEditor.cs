using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Archer.EditorTools
{
    /// <summary>
    /// 방 그리드 에디터 (메뉴 Archer/방 그리드 에디터).
    /// 방 프리팹을 프리팹 모드로 열고(또는 씬의 방을 선택하고) 붓을 고른 뒤 씬 뷰의 칸을 클릭/드래그해서 찍는다.
    /// 장애물(2x2) · 장애물 영역 · 구멍 · 몬스터 스폰 · 입구 · 출구 · 지우개.
    /// </summary>
    public class RoomGridEditor : EditorWindow
    {
        enum Brush { None, Block, Hole, Monster, Entry, Exit, Erase }

        static readonly string[] BrushNames = { "선택", "장애물 영역", "구멍", "몬스터", "입구", "출구", "지우개" };
        static readonly Dictionary<EnemyKind, string> KindNames = new()
        {
            { EnemyKind.Slime, "슬라임" }, { EnemyKind.Bat, "박쥐" }, { EnemyKind.Archer, "원거리" },
            { EnemyKind.Charger, "돌진" }, { EnemyKind.Turret, "포탑(꽃)" }, { EnemyKind.MiniBoss, "중간보스" },
            { EnemyKind.Boss, "보스" },
        };

        Brush brush = Brush.None;
        EnemyKind kind = EnemyKind.Slime;
        bool showOverlay = true;
        Vector2Int? hoverCell;
        Vector2Int lastPainted = new(-99, -99);

        [MenuItem("Archer/방 그리드 에디터", false, 10)]
        static void Open() => GetWindow<RoomGridEditor>("방 그리드 에디터");

        void OnEnable() => SceneView.duringSceneGui += OnSceneGUI;
        void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;
        void OnSelectionChange() => Repaint();

        // ───────── 대상 방 ─────────

        static RoomRoot Target()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null) return stage.prefabContentsRoot.GetComponent<RoomRoot>();
            return Selection.activeGameObject ? Selection.activeGameObject.GetComponentInParent<RoomRoot>() : null;
        }

        static GameConfig Config()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Game/Data/GameConfig.asset");
            if (cfg) return cfg;
            // 자리를 옮겼어도 찾게
            var guid = AssetDatabase.FindAssets("t:GameConfig").FirstOrDefault();
            return guid != null ? AssetDatabase.LoadAssetAtPath<GameConfig>(AssetDatabase.GUIDToAssetPath(guid)) : null;
        }

        // ───────── 창 ─────────

        void OnGUI()
        {
            var room = Target();
            EditorGUILayout.Space(4);
            if (room == null)
            {
                EditorGUILayout.HelpBox("방 프리팹(Prefabs/Rooms)을 더블클릭해서 프리팹 모드로 열거나,\n씬에 올린 방을 선택하세요.", MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField("편집 중", room.name, EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var type = (RoomType)EditorGUILayout.EnumPopup("방 종류", room.type);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(room, "방 종류");
                room.type = type;
                MarkDirty(room);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("붓", EditorStyles.boldLabel);
            brush = (Brush)GUILayout.SelectionGrid((int)brush, BrushNames, 4);

            switch (brush)
            {
                case Brush.Hole:
                    EditorGUILayout.HelpBox("칸을 클릭/드래그해서 구멍을 뚫어요. 이동은 막고 화살은 통과해요.", MessageType.None);
                    break;
                case Brush.Block:
                    EditorGUILayout.HelpBox("배경에 직접 만든 장애물 자리를 칸 단위로 칠해요 (보이지 않는 판정).\n이동을 막고 화살도 막아요. '벽 반사' 스킬이면 튕겨요.", MessageType.None);
                    break;
                case Brush.Monster:
                    var names = KindNames.Values.ToArray();
                    var keys = KindNames.Keys.ToArray();
                    int idx = System.Array.IndexOf(keys, kind);
                    kind = keys[EditorGUILayout.Popup("몬스터", Mathf.Max(0, idx), names)];
                    break;
                case Brush.Entry:
                    EditorGUILayout.HelpBox("아무 칸이나 클릭하세요 (2칸). 벽 줄 위면 플레이어는 그 바로 안쪽에서, 방 안이면 그 자리에서 시작해요.", MessageType.None);
                    break;
                case Brush.Exit:
                    EditorGUILayout.HelpBox("아무 칸이나 클릭하세요 (2칸). 벽 줄 위면 그 자리 벽에 문이 생기고, 방 안이면 그 자리에 출구 포털이 생겨요.", MessageType.None);
                    break;
                case Brush.Erase:
                    EditorGUILayout.HelpBox("클릭/드래그한 칸의 장애물·장애물 영역·구멍·몬스터를 지워요.", MessageType.None);
                    break;
            }

            EditorGUILayout.Space(6);
            showOverlay = EditorGUILayout.Toggle("칸 표시", showOverlay);

            EditorGUILayout.Space(6);
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            bool dirty = stage != null ? stage.scene.isDirty : PrefabUtility.HasPrefabInstanceAnyOverrides(PrefabUtility.GetOutermostPrefabInstanceRoot(room.gameObject) ?? room.gameObject, false);
            using (new EditorGUI.DisabledScope(!dirty))
                if (GUILayout.Button(dirty ? "저장 (방 프리팹에 반영)" : "저장됨", GUILayout.Height(28))) Save(room);

            var s = Scan(room);
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"장애물 {s.obstacles.Count}  ·  장애물 영역 {s.blocks.Count}칸  ·  구멍 {s.holes.Count}  ·  몬스터 {s.spawns.Count}");
            if (brush != Brush.None)
                EditorGUILayout.HelpBox("씬 뷰에서 찍는 중에는 오브젝트 선택이 안 돼요. 끝나면 '선택'을 누르세요.", MessageType.None);
        }

        // ───────── 현재 방 상태 ─────────

        class State
        {
            // 예전 2x2 장애물 오브젝트 (새로 놓지는 않고, 보여주고 지우개로 지울 수만 있음)
            public readonly Dictionary<Vector2Int, Transform> obstacleCells = new();
            public readonly List<Transform> obstacles = new();
            public readonly HashSet<Vector2Int> holes = new();
            public readonly HashSet<Vector2Int> blocks = new();
            public readonly Dictionary<Vector2Int, Transform> spawns = new();

            /// <summary>무언가 차지한 칸 (장애물·장애물 영역·구멍)</summary>
            public bool Solid(Vector2Int c) => obstacleCells.ContainsKey(c) || blocks.Contains(c) || holes.Contains(c);
        }

        static State Scan(RoomRoot room)
        {
            var s = new State();
            var t = room.transform;
            var ob = t.Find("Obstacles");
            if (ob)
                foreach (Transform o in ob)
                {
                    if (o.name.StartsWith("Lava"))
                    {
                        s.holes.Add(RoomGrid.ToCell(o.localPosition));
                        continue;
                    }
                    int n = o.TryGetComponent<BoxCollider>(out var box) ? Mathf.Max(1, Mathf.RoundToInt(box.size.x / RoomGrid.Cell)) : 1;
                    s.obstacles.Add(o);
                    foreach (var c in RoomGrid.BlockCells(o.localPosition, n)) s.obstacleCells[c] = o;
                }
            var holes = t.Find("Holes");
            if (holes && holes.TryGetComponent<FloorHoles>(out var fh)) s.holes.UnionWith(fh.Cells());
            var bl = t.Find("Blockers");
            if (bl)
            {
                if (bl.TryGetComponent<GridArea>(out var area)) s.blocks.UnionWith(area.cells);
                foreach (Transform b in bl)
                    if (!b.name.StartsWith("_")) s.blocks.Add(RoomGrid.ToCell(b.localPosition)); // 예전 칸마다 오브젝트
            }
            var sp = t.Find("Spawns");
            if (sp)
                foreach (Transform x in sp) s.spawns[RoomGrid.ToCell(x.localPosition)] = x;
            return s;
        }

        /// <summary>
        /// 예전 방식(칸마다 오브젝트 하나)의 구멍·장애물 영역을 칸 목록(GridArea)으로 옮기고 합친 판정으로 바꾼다.
        /// 옮길 게 있었으면 true
        /// </summary>
        public static bool Migrate(RoomRoot room, bool undo)
        {
            bool any = false;
            foreach (var name in new[] { "Holes", "Blockers" })
            {
                var g = room.transform.Find(name);
                if (!g) continue;
                var legacy = new List<Transform>();
                foreach (Transform c in g)
                    if (!c.name.StartsWith("_")) legacy.Add(c);
                bool noArea = !g.TryGetComponent<GridArea>(out _);
                if (legacy.Count == 0 && !noArea) continue;
                if (undo) Undo.RegisterFullObjectHierarchyUndo(g.gameObject, "칸 영역으로 바꾸기");
                var area = name == "Holes" ? RoomBuilder.HoleArea(room) : RoomBuilder.BlockArea(room);
                foreach (var c in legacy)
                {
                    area.Add(RoomGrid.ToCell(c.localPosition));
                    Object.DestroyImmediate(c.gameObject);
                }
                area.RebuildColliders();
                any = true;
            }
            return any;
        }

        /// <summary>칸 목록을 바꾸고 판정을 다시 만든다 (되돌리기 포함)</summary>
        static void EditArea(GridArea area, string label, System.Func<GridArea, bool> edit)
        {
            Undo.RegisterFullObjectHierarchyUndo(area.gameObject, label);
            if (!edit(area)) return;
            area.RebuildColliders();
            if (area.TryGetComponent<FloorHoles>(out var fh)) fh.Rebuild();
        }

        // ───────── 씬 뷰 ─────────

        void OnSceneGUI(SceneView view)
        {
            var room = Target();
            if (room == null) return;
            var e = Event.current;
            var m = room.transform.localToWorldMatrix;

            if (showOverlay && e.type == EventType.Repaint) DrawOverlay(room, m);

            if (brush == Brush.None) { hoverCell = null; return; }
            int id = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(id);

            // 마우스 → 방 바닥(y=0) 평면
            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            var plane = new Plane(room.transform.up, room.transform.position);
            if (!plane.Raycast(ray, out float dist)) return;
            var local = room.transform.InverseTransformPoint(ray.GetPoint(dist));
            var cell = RoomGrid.ToCell(local);
            if (hoverCell != cell) { hoverCell = cell; view.Repaint(); }

            if (e.type == EventType.Repaint) DrawHover(room, m, local, cell);

            bool click = e.type == EventType.MouseDown && e.button == 0 && !e.alt;
            bool drag = e.type == EventType.MouseDrag && e.button == 0 && !e.alt;
            if (click) lastPainted = new Vector2Int(-99, -99);
            if ((click || (drag && (brush == Brush.Hole || brush == Brush.Erase || brush == Brush.Block))) && cell != lastPainted)
            {
                lastPainted = cell;
                Apply(room, local, cell);
                e.Use();
            }
            if (e.type == EventType.MouseUp && e.button == 0) lastPainted = new Vector2Int(-99, -99);
        }

        void Apply(RoomRoot room, Vector3 local, Vector2Int cell)
        {
            var cfg = Config();
            bool changed = Migrate(room, true);
            var s = Scan(room);
            switch (brush)
            {
                case Brush.Hole:
                {
                    if (!RoomGrid.IsInner(cell) || s.Solid(cell) || Reserved(room).Contains(cell)) break;
                    if (!room.transform.Find("Holes")) Undo.RegisterCreatedObjectUndo(RoomBuilder.EnsureHoles(cfg, room).gameObject, "구멍 뚫기");
                    RoomBuilder.EnsureHoles(cfg, room);
                    EditArea(RoomBuilder.HoleArea(room), "구멍 뚫기", a => a.Add(cell));
                    changed = true;
                    break;
                }
                case Brush.Block:
                {
                    if (!RoomGrid.IsInner(cell) || s.Solid(cell) || s.spawns.ContainsKey(cell) || Reserved(room).Contains(cell)) break;
                    if (!room.transform.Find("Blockers")) Undo.RegisterCreatedObjectUndo(RoomBuilder.BlockArea(room).gameObject, "장애물 영역");
                    EditArea(RoomBuilder.BlockArea(room), "장애물 영역", a => a.Add(cell));
                    changed = true;
                    break;
                }
                case Brush.Monster:
                {
                    if (!RoomGrid.IsInner(cell) || s.Solid(cell)) break;
                    if (s.spawns.TryGetValue(cell, out var old))
                    {
                        Undo.RecordObject(old.GetComponent<EnemySpawn>(), "몬스터 바꾸기");
                        old.GetComponent<EnemySpawn>().kind = kind;
                        Undo.RecordObject(old.gameObject, "몬스터 바꾸기");
                        old.name = $"Spawn_{kind}";
                    }
                    else
                    {
                        var sp = new GameObject($"Spawn_{kind}");
                        sp.transform.SetParent(Group(room, "Spawns"), false);
                        sp.transform.localPosition = RoomGrid.Center(cell.x, cell.y);
                        sp.AddComponent<EnemySpawn>().kind = kind;
                        Undo.RegisterCreatedObjectUndo(sp, "몬스터 배치");
                    }
                    changed = true;
                    break;
                }
                case Brush.Entry:
                {
                    var pos = (Vector3?)RoomBuilder.MarkerPosition(local);
                    if (MarkerBlocked(room, s, pos.Value, room.exit)) break;
                    if (!room.entry) { Undo.RecordObject(room, "입구"); RoomBuilder.EnsureMarkers(room); Undo.RegisterCreatedObjectUndo(room.entry.gameObject, "입구"); }
                    Undo.RecordObject(room.entry, "입구 옮기기");
                    room.entry.localPosition = pos.Value;
                    changed = true;
                    break;
                }
                case Brush.Exit:
                {
                    var pos = (Vector3?)RoomBuilder.MarkerPosition(local);
                    if (MarkerBlocked(room, s, pos.Value, room.entry)) break;
                    Undo.RegisterFullObjectHierarchyUndo(room.gameObject, "출구 옮기기");
                    if (!room.exit) RoomBuilder.EnsureMarkers(room);
                    room.exit.localPosition = pos.Value;
                    RoomBuilder.BuildWalls(cfg, room, (pf, p) => (GameObject)PrefabUtility.InstantiatePrefab(pf, p));
                    changed = true;
                    break;
                }
                case Brush.Erase:
                {
                    if (s.obstacleCells.TryGetValue(cell, out var o)) { Undo.DestroyObjectImmediate(o.gameObject); changed = true; }
                    if (s.holes.Contains(cell) && room.transform.Find("Holes"))
                    {
                        EditArea(RoomBuilder.HoleArea(room), "구멍 지우기", a => a.Remove(cell));
                        changed = true;
                    }
                    if (s.blocks.Contains(cell) && room.transform.Find("Blockers"))
                    {
                        EditArea(RoomBuilder.BlockArea(room), "장애물 영역 지우기", a => a.Remove(cell));
                        changed = true;
                    }
                    if (s.spawns.TryGetValue(cell, out var sp)) { Undo.DestroyObjectImmediate(sp.gameObject); changed = true; }
                    break;
                }
            }
            if (changed)
            {
                if (room.TryGetComponent<RoomFloorAO>(out var ao)) ao.Rebuild(); // 바닥 접지 AO 갱신
                MarkDirty(room);
            }
        }

        /// <summary>입구·출구·시작 지점이 차지한 칸 (장애물·구멍 금지)</summary>
        static HashSet<Vector2Int> Reserved(RoomRoot room)
        {
            var set = new HashSet<Vector2Int>();
            if (room.entry)
            {
                foreach (var c in RoomBuilder.MarkerCells(room.entry.localPosition)) set.Add(c);
                set.Add(RoomGrid.ToCell(RoomBuilder.EntrySpawnPoint(room.entry.localPosition)));
            }
            if (room.exit) foreach (var c in RoomBuilder.MarkerCells(room.exit.localPosition)) set.Add(c);
            return set;
        }

        /// <summary>2칸 표식 자리가 장애물·구멍·다른 표식과 겹치는지</summary>
        static bool MarkerBlocked(RoomRoot room, State s, Vector3 pos, Transform other)
        {
            var cells = RoomBuilder.MarkerCells(pos);
            if (cells.Any(c => s.Solid(c))) return true;
            if (other && RoomBuilder.MarkerCells(other.localPosition).Intersect(cells).Any()) return true;
            return false;
        }

        /// <summary>(예전) 바깥 벽 줄 위의 2칸 문 자리</summary>
        static Vector3? ExitPosition(Vector3 local)
        {
            float c = RoomGrid.Cell;
            float ox = RoomGrid.Width * 0.5f - c * 0.5f, oz = RoomGrid.Length * 0.5f - c * 0.5f;
            int side = RoomBuilder.ExitSide(local);
            if (side <= 1)
            {
                float lim = RoomGrid.InnerHalfW - c; // 문 2칸이 안쪽 폭 안에 들어오게
                float x = Mathf.Clamp(Mathf.Round(local.x / c) * c, -lim, lim);
                return new Vector3(x, 0f, side == 0 ? oz : -oz);
            }
            float limZ = RoomGrid.InnerHalfL - c;
            float z = Mathf.Clamp(Mathf.Round(local.z / c) * c, -limZ, limZ);
            return new Vector3(side == 2 ? -ox : ox, 0f, z);
        }

        static Transform Group(RoomRoot room, string name)
        {
            var t = room.transform.Find(name);
            if (t) return t;
            var g = new GameObject(name).transform;
            g.SetParent(room.transform, false);
            Undo.RegisterCreatedObjectUndo(g.gameObject, "그룹");
            return g;
        }

        /// <summary>프리팹 모드면 프리팹을 저장, 씬에 올린 방이면 바뀐 내용을 프리팹에 적용</summary>
        static void Save(RoomRoot room)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, stage.assetPath, out bool ok);
                if (ok)
                {
                    // 프리팹 모드의 '수정됨' 표시도 지운다 (닫을 때 다시 묻지 않게)
                    var clear = stage.GetType().GetMethod("ClearDirtiness",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                    clear?.Invoke(stage, null);
                    Debug.Log($"[Archer] 저장: {stage.assetPath}");
                }
                else Debug.LogError($"[Archer] 저장 실패: {stage.assetPath} (콘솔의 다른 오류를 확인하세요)");
                return;
            }
            var inst = PrefabUtility.GetOutermostPrefabInstanceRoot(room.gameObject);
            if (inst)
            {
                PrefabUtility.ApplyPrefabInstance(inst, InteractionMode.UserAction);
                Debug.Log($"[Archer] 프리팹에 적용: {PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(inst)}");
            }
            else Debug.LogWarning("[Archer] 이 방은 프리팹이 아니라서 저장할 프리팹이 없어요.");
        }

        static void MarkDirty(RoomRoot room)
        {
            EditorUtility.SetDirty(room);
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null) EditorSceneManager.MarkSceneDirty(stage.scene);
            else EditorSceneManager.MarkSceneDirty(room.gameObject.scene);
        }

        // ───────── 그리기 ─────────

        static void Quad(Matrix4x4 m, Vector3 center, float w, float l, Color fill, Color line)
        {
            float hw = w * 0.5f, hl = l * 0.5f;
            var y = Vector3.up * 0.02f;
            var v = new[]
            {
                m.MultiplyPoint3x4(center + new Vector3(-hw, 0, -hl) + y), m.MultiplyPoint3x4(center + new Vector3(-hw, 0, hl) + y),
                m.MultiplyPoint3x4(center + new Vector3(hw, 0, hl) + y), m.MultiplyPoint3x4(center + new Vector3(hw, 0, -hl) + y),
            };
            Handles.DrawSolidRectangleWithOutline(v, fill, line);
        }

        static void DrawOverlay(RoomRoot room, Matrix4x4 m)
        {
            var s = Scan(room);
            float c = RoomGrid.Cell;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            // 벽 줄
            for (int x = 0; x < RoomGrid.Cols; x++)
            for (int y = 0; y < RoomGrid.Rows; y++)
            {
                var cell = new Vector2Int(x, y);
                if (RoomGrid.IsInner(cell)) continue;
                Quad(m, RoomGrid.Center(x, y), c, c, new Color(1f, 0.3f, 0.3f, 0.12f), new Color(1f, 0.3f, 0.3f, 0.25f));
            }
            foreach (var h in s.holes)
                Quad(m, RoomGrid.Center(h.x, h.y), c, c, new Color(0.2f, 0.7f, 1f, 0.45f), new Color(0.2f, 0.7f, 1f, 0.9f));
            foreach (var b in s.blocks)
                Quad(m, RoomGrid.Center(b.x, b.y), c, c, new Color(0.75f, 0.35f, 1f, 0.45f), new Color(0.85f, 0.6f, 1f, 0.9f));
            foreach (var o in s.obstacles)
            {
                int n = o.TryGetComponent<BoxCollider>(out var box) ? Mathf.Max(1, Mathf.RoundToInt(box.size.x / c)) : 1;
                Quad(m, o.localPosition, c * n, c * n, new Color(0.5f, 0.5f, 0.5f, 0.35f), new Color(1f, 1f, 1f, 0.8f));
            }
            foreach (var kv in s.spawns)
            {
                var sp = kv.Value.GetComponent<EnemySpawn>();
                bool boss = sp && (sp.kind == EnemyKind.Boss || sp.kind == EnemyKind.MiniBoss);
                var col = boss ? new Color(1f, 0.2f, 0.2f, 0.6f) : new Color(1f, 0.55f, 0.2f, 0.55f);
                Quad(m, kv.Value.localPosition, c * 0.8f, c * 0.8f, col, Color.white);
                if (sp) Handles.Label(m.MultiplyPoint3x4(kv.Value.localPosition + Vector3.up * 0.3f),
                    KindNames.TryGetValue(sp.kind, out var nm) ? nm : sp.kind.ToString(), EditorStyles.whiteMiniLabel);
            }
            if (room.entry)
            {
                bool ev = !RoomBuilder.IsVerticalMarker(room.entry.localPosition);
                Quad(m, room.entry.localPosition, ev ? c * 2 : c, ev ? c : c * 2, new Color(0.3f, 1f, 0.4f, 0.6f), Color.white);
                Handles.Label(m.MultiplyPoint3x4(room.entry.localPosition + Vector3.up * 0.3f), "입구", EditorStyles.whiteMiniLabel);
                // 플레이어 시작 지점
                Handles.color = new Color(0.3f, 1f, 0.4f, 0.9f);
                Handles.DrawWireDisc(m.MultiplyPoint3x4(RoomBuilder.EntrySpawnPoint(room.entry.localPosition) + Vector3.up * 0.03f),
                    room.transform.up, c * 0.35f);
            }
            if (room.exit)
            {
                bool horiz = !RoomBuilder.IsVerticalMarker(room.exit.localPosition);
                Quad(m, room.exit.localPosition, horiz ? c * 2 : c, horiz ? c : c * 2, new Color(1f, 0.9f, 0.2f, 0.6f), Color.white);
                Handles.Label(m.MultiplyPoint3x4(room.exit.localPosition + Vector3.up * 0.3f), "출구", EditorStyles.whiteMiniLabel);
            }
        }

        void DrawHover(RoomRoot room, Matrix4x4 m, Vector3 local, Vector2Int cell)
        {
            float c = RoomGrid.Cell;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            var ok = new Color(1f, 1f, 1f, 0.25f);
            switch (brush)
            {
                case Brush.Exit:
                case Brush.Entry:
                    var p = (Vector3?)RoomBuilder.MarkerPosition(local);
                    if (p != null)
                    {
                        bool v = !RoomBuilder.IsVerticalMarker(p.Value);
                        var col = brush == Brush.Exit ? new Color(1f, 0.9f, 0.2f, 0.35f) : new Color(0.3f, 1f, 0.4f, 0.35f);
                        Quad(m, p.Value, v ? c * 2 : c, v ? c : c * 2, col, brush == Brush.Exit ? Color.yellow : Color.green);
                    }
                    break;
                default:
                    if (cell.x >= 0 && cell.x < RoomGrid.Cols && cell.y >= 0 && cell.y < RoomGrid.Rows)
                        Quad(m, RoomGrid.Center(cell.x, cell.y), c, c, brush == Brush.Erase ? new Color(1f, 0.3f, 0.3f, 0.35f) : ok, Color.white);
                    break;
            }
        }
    }
}

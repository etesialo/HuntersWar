using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Archer
{
    /// <summary>
    /// 배치도 문자열로 방을 만든다. 에디터에서 방 프리팹을 처음 만들 때 쓰고,
    /// 방 프리팹이 없을 때는 게임 중에도 이걸로 만든다.
    /// 장애물·용암은 판정(루트)과 겉모습(자식)을 한 오브젝트로 묶어서, 옮기면 같이 움직인다.
    /// </summary>
    public static class RoomBuilder
    {
        /// <summary>배치도 칸 수 = 안쪽 그리드 (RoomGrid 16 x 26, 한 칸 0.75m)</summary>
        public const int Cols = RoomGrid.InnerCols;
        public const int Rows = RoomGrid.InnerRows;
        const float WallH = 1.6f;
        /// <summary>바닥 판 크기 = 전체 그리드 (13.5 x 21)</summary>
        public const float FloorW = RoomGrid.Width;
        public const float FloorL = RoomGrid.Length;
        /// <summary>문 폭 (가운데 2칸)</summary>
        public const float DoorW = RoomGrid.Cell * 2f;
        /// <summary>장애물 기본 크기: 2 x 2칸 (1.5m). 판정은 살짝 작게</summary>
        public const int ObstacleCells = RoomGrid.ObstacleCells;
        public const float ObstacleSpan = RoomGrid.Cell * ObstacleCells;
        public const float ObstacleSize = ObstacleSpan * 0.96f;

        /// <param name="spawn">프롭 생성 방식. 에디터는 프리팹 링크를 살리려고 PrefabUtility 를 넘긴다.</param>
        public static RoomRoot Build(GameConfig cfg, string[] rows, RoomType type, string name = "Room",
            Func<GameObject, Transform, GameObject> spawn = null)
        {
            spawn ??= (prefab, parent) => Object.Instantiate(prefab, parent);
            float hl = StageManager.HalfL;
            var root = new GameObject(name);
            var t = root.transform;
            var room = root.AddComponent<RoomRoot>();
            room.type = type;
            root.AddComponent<RoomFloorAO>();

            BuildFloor(cfg, t, spawn);
            EnsureMarkers(room);
            BuildWalls(cfg, room, spawn);

            var theme = cfg.theme;
            var rng = new System.Random(LayoutSeed(rows));
            if (theme)
            {
                // 문 양옆 기둥 + 위쪽 벽 뒤 배경 (벽 뒤라 판정 없음)
                var deco = Group(t, "Deco");
                if (theme.doorPillar)
                {
                    float px = DoorW * 0.5f + RoomGrid.Cell * 0.5f;
                    float pz = hl + RoomGrid.Cell * 0.5f;
                    Prop(deco, spawn, theme.doorPillar, new Vector3(-px, 0f, pz), 0.8f, 180f);
                    Prop(deco, spawn, theme.doorPillar, new Vector3(px, 0f, pz), 0.8f, 180f);
                }
                if (theme.topGlow)
                {
                    var glow = spawn(theme.topGlow, deco);
                    glow.name = "TopGlow";
                    glow.transform.localPosition = theme.topGlowPos;
                    glow.transform.localScale = Vector3.one * theme.topGlowScale;
                    if (theme.topGlowMaterial)
                        foreach (var r in glow.GetComponentsInChildren<Renderer>()) r.sharedMaterial = theme.topGlowMaterial;
                }
                if (theme.backdrop != null && theme.backdrop.Length > 0)
                {
                    float[] xs = { -4.6f, -3.1f, 3.1f, 4.6f };
                    for (int i = 0; i < xs.Length; i++)
                        Prop(deco, spawn, theme.backdrop[i % theme.backdrop.Length], new Vector3(xs[i], 0f, hl + 2f), 2f, rng.Next(4) * 90f);
                }
            }

            // 칸 내용
            var obstacles = Group(t, "Obstacles");
            var spawns = Group(t, "Spawns");
            var rockCells = new bool[Rows, Cols];
            for (int r = 0; r < rows.Length && r < Rows; r++)
            {
                var row = rows[r];
                for (int c = 0; c < row.Length && c < Cols; c++)
                {
                    char ch = row[c];
                    var pos = CellPos(r, c);
                    switch (ch)
                    {
                        case '.':
                        case ' ':
                            break;
                        case '#':
                            rockCells[r, c] = true; // 아래에서 2 x 2 블록으로 묶는다
                            break;
                        case '~':
                            MakeHole(cfg, room, pos);
                            break;
                        default:
                            if (cfg.GetEnemy(ch) == null) break;
                            var sp = new GameObject($"Spawn_{(EnemyKind)ch}");
                            sp.transform.SetParent(spawns, false);
                            sp.transform.localPosition = pos;
                            sp.AddComponent<EnemySpawn>().kind = (EnemyKind)ch;
                            break;
                    }
                }
            }
            // '#' 칸들을 2 x 2 장애물로 덮는다 (위→아래, 왼→오른쪽 순으로 욕심껏)
            if (cfg.autoObstacles)
            foreach (var center in TileBlocks(rockCells))
            {
                GameObject visual = theme && theme.obstacles != null && theme.obstacles.Length > 0
                    ? theme.obstacles[rng.Next(theme.obstacles.Length)] : null;
                MakeObstacle(cfg, obstacles, spawn, center, visual, rng.Next(4) * 90f);
            }
            // 구멍 칸들을 합친 판정
            if (t.Find("Holes")) HoleArea(room).RebuildColliders();
            return room;
        }

        /// <summary>
        /// 채워진 칸(안쪽 그리드 [row, col])을 겹치지 않는 n x n 블록으로 덮고 블록 중심들을 돌려준다.
        /// 블록이 빈 칸을 조금 넘어 덮을 수는 있다. 방 프리팹 이전에도 쓴다.
        /// </summary>
        public static System.Collections.Generic.List<Vector3> TileBlocks(bool[,] filled, int n = RoomGrid.ObstacleCells)
        {
            int R = filled.GetLength(0), C = filled.GetLength(1);
            var used = new bool[R, C];
            var result = new System.Collections.Generic.List<Vector3>();
            for (int r = 0; r < R; r++)
            for (int c = 0; c < C; c++)
            {
                if (!filled[r, c] || used[r, c]) continue;
                // 이 칸을 포함하는 블록 위치 후보 (기본: 이 칸이 왼쪽 위). 이미 쓴 칸과 겹치면 다음 후보
                int r0 = -1, c0 = -1;
                for (int k = 0; k < n * n && r0 < 0; k++)
                {
                    int tr = Mathf.Clamp(r - k / n, 0, R - n), tc = Mathf.Clamp(c - k % n, 0, C - n);
                    bool free = true;
                    for (int dr = 0; dr < n && free; dr++)
                        for (int dc = 0; dc < n && free; dc++)
                            if (used[tr + dr, tc + dc]) free = false;
                    if (free) { r0 = tr; c0 = tc; }
                }
                if (r0 < 0) { used[r, c] = true; continue; }
                for (int dr = 0; dr < n; dr++)
                    for (int dc = 0; dc < n; dc++)
                        used[r0 + dr, c0 + dc] = true;
                var a = CellPos(r0, c0);
                var b = CellPos(r0 + n - 1, c0 + n - 1);
                result.Add((a + b) * 0.5f);
            }
            return result;
        }

        /// <summary>
        /// 외벽 판정: 그리드 가장 바깥 한 줄(두께 0.75m). 출구(room.exit) 자리에 2칸 틈을 내고 거기에 문을 둔다.
        /// 기존 Walls/Door/Portal 은 지우고 다시 만든다.
        /// cfg.wallModel(통메시 테두리)이 있으면 그게 겉모습이고, 벽·문 큐브는 판정만 남긴다(안 보임).
        /// </summary>
        public static void BuildWalls(GameConfig cfg, RoomRoot room, Func<GameObject, Transform, GameObject> spawn = null)
        {
            spawn ??= (prefab, parent) => Object.Instantiate(prefab, parent);
            var t = room.transform;
            GameObject keepMesh = null;
            var oldWalls = t.Find("Walls");
            if (oldWalls)
            {
                var m = oldWalls.Find("WallMesh");
                if (m) { keepMesh = m.gameObject; m.SetParent(t, true); } // 테두리 메시는 살린다
                Object.DestroyImmediate(oldWalls.gameObject);
            }
            foreach (var n in new[] { "Door", "Portal" })
            {
                var old = t.Find(n);
                if (old) Object.DestroyImmediate(old.gameObject);
            }

            float c = RoomGrid.Cell;
            float ox = RoomGrid.Width * 0.5f - c * 0.5f;   // 6.375
            float oz = RoomGrid.Length * 0.5f - c * 0.5f;  // 10.125
            var walls = Group(t, "Walls");
            walls.SetSiblingIndex(Mathf.Min(1, t.childCount - 1));

            // 출구가 있는 변과 그 변 위의 위치
            var ex = room.exit ? room.exit.localPosition : new Vector3(0f, 0f, oz);
            bool exitOnWall = OnWallRing(ex);
            int side = exitOnWall ? ExitSide(ex) : -1; // 0 위, 1 아래, 2 왼쪽, 3 오른쪽, -1 방 안(틈 없음)
            float gapCenter = side <= 1 ? ex.x : ex.z;

            // 위/아래 변은 전체 폭, 좌/우 변은 모서리 제외 길이
            WallSide(cfg, walls, "WallT", true, oz, -RoomGrid.Width * 0.5f, RoomGrid.Width * 0.5f, side == 0 ? gapCenter : (float?)null);
            WallSide(cfg, walls, "WallB", true, -oz, -RoomGrid.Width * 0.5f, RoomGrid.Width * 0.5f, side == 1 ? gapCenter : (float?)null);
            WallSide(cfg, walls, "WallL", false, -ox, -oz + c * 0.5f, oz - c * 0.5f, side == 2 ? gapCenter : (float?)null);
            WallSide(cfg, walls, "WallR", false, ox, -oz + c * 0.5f, oz - c * 0.5f, side == 3 ? gapCenter : (float?)null);

            // 문: 틈을 막는 판정, 포털: 틈 바로 안쪽 바닥. 출구가 방 안이면 문 없이 그 자리에 포털
            Vector3 doorPos, doorSize, portalPos, portalSize;
            if (side < 0)
            {
                doorPos = Vector3.zero; doorSize = Vector3.zero;
                portalPos = new Vector3(ex.x, 0.03f, ex.z);
                portalSize = new Vector3(DoorW, 0.05f, c);
            }
            else if (side <= 1)
            {
                float z = side == 0 ? oz : -oz;
                doorPos = new Vector3(gapCenter, WallH * 0.5f, z);
                doorSize = new Vector3(DoorW, WallH, c);
                portalPos = new Vector3(gapCenter, 0.03f, z + (side == 0 ? -c : c));
                portalSize = new Vector3(DoorW, 0.05f, c);
            }
            else
            {
                float x = side == 2 ? -ox : ox;
                doorPos = new Vector3(x, WallH * 0.5f, gapCenter);
                doorSize = new Vector3(c, WallH, DoorW);
                portalPos = new Vector3(x + (side == 2 ? c : -c), 0.03f, gapCenter);
                portalSize = new Vector3(c, 0.05f, DoorW);
            }
            if (side >= 0)
            {
                room.door = Block(t, "Door", doorPos, doorSize, cfg.door, true, true);
                room.door.transform.SetSiblingIndex(walls.GetSiblingIndex() + 1);
            }
            else room.door = null;
            room.portal = Block(t, "Portal", portalPos, portalSize, cfg.portal, false, false, false);
            room.portal.transform.SetSiblingIndex(walls.GetSiblingIndex() + 2);
            room.portal.SetActive(false);
            room.portal.GetComponent<Renderer>().enabled = false; // 게임 화면엔 안 보임 (판정은 출구 위치로)

            if (keepMesh) keepMesh.transform.SetParent(walls, true);
            else if (cfg.wallModel)
            {
                keepMesh = spawn(cfg.wallModel, walls);
                keepMesh.name = "WallMesh";
                keepMesh.transform.localPosition = Vector3.zero;
            }
            if (keepMesh)
            {
                keepMesh.transform.SetAsFirstSibling();
                foreach (Transform b in walls)
                    if (b != keepMesh.transform && b.TryGetComponent<Renderer>(out var r)) r.enabled = false;
                if (room.door) room.door.GetComponent<Renderer>().enabled = false;
            }
        }

        /// <summary>입구/출구 표식이 벽 줄 위에 있는지 (아니면 방 안)</summary>
        public static bool OnWallRing(Vector3 p) =>
            Mathf.Abs(p.x) > RoomGrid.InnerHalfW + 0.01f || Mathf.Abs(p.z) > RoomGrid.InnerHalfL + 0.01f;

        /// <summary>2칸 표식이 세로(1 x 2)인지: 좌/우 벽 줄 위일 때만 세로</summary>
        public static bool IsVerticalMarker(Vector3 p) =>
            Mathf.Abs(p.x) > RoomGrid.InnerHalfW + 0.01f && Mathf.Abs(p.z) <= RoomGrid.InnerHalfL + 0.01f;

        /// <summary>
        /// 클릭 위치 → 2칸 표식 위치. 가로 2칸(중심은 칸 경계), 좌/우 벽 줄이면 세로 2칸.
        /// 그리드 어디든 가능 (모서리 칸은 제외).
        /// </summary>
        public static Vector3 MarkerPosition(Vector3 local)
        {
            float c = RoomGrid.Cell;
            var cell = RoomGrid.ToCell(local);
            cell.x = Mathf.Clamp(cell.x, 0, RoomGrid.Cols - 1);
            cell.y = Mathf.Clamp(cell.y, 1, RoomGrid.Rows - 2);
            var center = RoomGrid.Center(cell.x, cell.y);
            bool sideRing = cell.x == 0 || cell.x == RoomGrid.Cols - 1;
            if (sideRing)
            {
                float limZ = RoomGrid.InnerHalfL - c;
                float z = Mathf.Clamp(Mathf.Round(local.z / c) * c, -limZ, limZ);
                return new Vector3(center.x, 0f, z);
            }
            float limX = RoomGrid.Width * 0.5f - c * 2f; // 좌우 벽 줄에 걸치지 않게
            float x = Mathf.Clamp(Mathf.Round(local.x / c) * c, -limX, limX);
            return new Vector3(x, 0f, center.z);
        }

        /// <summary>2칸 표식이 덮는 칸들</summary>
        public static Vector2Int[] MarkerCells(Vector3 p)
        {
            float h = RoomGrid.Cell * 0.5f;
            var d = IsVerticalMarker(p) ? new Vector3(0f, 0f, h) : new Vector3(h, 0f, 0f);
            return new[] { RoomGrid.ToCell(p - d), RoomGrid.ToCell(p + d) };
        }

        /// <summary>입구에서 플레이어가 서는 곳: 벽 줄 위면 바로 안쪽, 방 안이면 그 자리</summary>
        public static Vector3 EntrySpawnPoint(Vector3 entryLocal)
        {
            if (!OnWallRing(entryLocal)) return entryLocal;
            float inward = RoomGrid.Cell * 1.4f;
            return ExitSide(entryLocal) switch
            {
                0 => entryLocal + new Vector3(0f, 0f, -inward),
                1 => entryLocal + new Vector3(0f, 0f, inward),
                2 => entryLocal + new Vector3(inward, 0f, 0f),
                _ => entryLocal + new Vector3(-inward, 0f, 0f),
            };
        }

        /// <summary>출구 위치가 어느 변인지: 0 위, 1 아래, 2 왼쪽, 3 오른쪽</summary>
        public static int ExitSide(Vector3 p)
        {
            float dz = Mathf.Abs(p.z) - RoomGrid.InnerHalfL, dx = Mathf.Abs(p.x) - RoomGrid.InnerHalfW;
            if (dz >= dx) return p.z >= 0f ? 0 : 1;
            return p.x < 0f ? 2 : 3;
        }

        /// <summary>한 변의 벽 판정. gap 이 있으면 그 위치에 문 폭만큼 틈을 낸다.</summary>
        static void WallSide(GameConfig cfg, Transform parent, string name, bool horizontal, float fixedCoord,
            float from, float to, float? gap)
        {
            float c = RoomGrid.Cell;
            void Seg(string n, float a, float b)
            {
                if (b - a < 0.01f) return;
                float mid = (a + b) * 0.5f, len = b - a;
                var pos = horizontal ? new Vector3(mid, WallH * 0.5f, fixedCoord) : new Vector3(fixedCoord, WallH * 0.5f, mid);
                var size = horizontal ? new Vector3(len, WallH, c) : new Vector3(c, WallH, len);
                Block(parent, n, pos, size, cfg.wall, true, true);
            }
            if (gap == null) { Seg(name, from, to); return; }
            float g0 = gap.Value - DoorW * 0.5f, g1 = gap.Value + DoorW * 0.5f;
            Seg(name + "_A", from, g0);
            Seg(name + "_B", g1, to);
        }

        /// <summary>입구/출구 표식이 없으면 기본 위치(아래쪽 가운데 / 위쪽 가운데 문)에 만든다</summary>
        public static void EnsureMarkers(RoomRoot room)
        {
            var t = room.transform;
            if (!room.entry)
            {
                var e = new GameObject("Entry").transform;
                e.SetParent(t, false);
                e.localPosition = new Vector3(0f, 0f, -(RoomGrid.Length * 0.5f - RoomGrid.Cell * 0.5f)); // 아래 벽 줄 가운데 2칸
                room.entry = e;
            }
            if (!room.exit)
            {
                var x = new GameObject("Exit").transform;
                x.SetParent(t, false);
                x.localPosition = new Vector3(0f, 0f, RoomGrid.Length * 0.5f - RoomGrid.Cell * 0.5f);
                room.exit = x;
            }
        }

        /// <summary>구멍 한 칸 (이동만 막음). Holes 묶음(FloorHoles)이 모양대로 마스크·단면·물을 만든다.</summary>
        /// <summary>구멍 칸 추가 (칸 목록에만 넣음. 판정은 HoleArea(room).RebuildColliders() 로 합쳐서 만든다)</summary>
        public static GridArea MakeHole(GameConfig cfg, RoomRoot room, Vector3 pos)
        {
            EnsureHoles(cfg, room);
            var area = HoleArea(room);
            area.Add(RoomGrid.ToCell(pos));
            return area;
        }

        /// <summary>구멍 칸 영역 (이동만 막고 화살은 통과)</summary>
        public static GridArea HoleArea(RoomRoot room)
        {
            var t = room.transform.Find("Holes");
            if (!t) t = Group(room.transform, "Holes");
            if (!t.TryGetComponent<GridArea>(out var area))
            {
                area = t.gameObject.AddComponent<GridArea>();
                area.blocksArrows = false;
            }
            return area;
        }

        /// <summary>장애물 영역 (배경에 직접 만든 장애물 자리. 이동·화살 모두 막음)</summary>
        public static GridArea BlockArea(RoomRoot room)
        {
            var t = room.transform.Find("Blockers");
            if (!t) t = Group(room.transform, "Blockers");
            if (!t.TryGetComponent<GridArea>(out var area))
            {
                area = t.gameObject.AddComponent<GridArea>();
                area.blocksArrows = true;
            }
            return area;
        }

        public static FloorHoles EnsureHoles(GameConfig cfg, RoomRoot room)
        {
            var t = room.transform.Find("Holes");
            if (!t) t = Group(room.transform, "Holes");
            var fh = t.GetComponent<FloorHoles>();
            if (!fh) fh = t.gameObject.AddComponent<FloorHoles>();
            if (cfg)
            {
                if (cfg.holeWall) fh.wallMaterial = cfg.holeWall;
                if (cfg.holeWater) fh.waterMaterial = cfg.holeWater;
                if (cfg.holeMask) fh.maskMaterial = cfg.holeMask;
            }
            return fh;
        }

        /// <summary>장애물(2 x 2칸): 판정(루트, 화살까지 막음) + 겉모습(자식). visual 이 없으면 큐브. pos 는 블록 중심.</summary>
        public static GameObject MakeObstacle(GameConfig cfg, Transform parent, Func<GameObject, Transform, GameObject> spawn,
            Vector3 pos, GameObject visual, float yaw)
        {
            var ob = Cell(parent, "Obstacle", pos, new Vector3(ObstacleSize, 1f, ObstacleSize), true);
            if (visual) Prop(ob.transform, spawn, visual, Vector3.zero, ObstacleSpan, yaw);
            else Block(ob.transform, "Rock", Vector3.up * 0.5f, new Vector3(ObstacleSize, 1f, ObstacleSize), cfg.obstacle, false, false);
            return ob;
        }

        /// <summary>용암 한 칸: 이동만 막는다 (Wall 없음 → 화살 통과)</summary>
        public static GameObject MakeLava(GameConfig cfg, Transform parent, Vector3 pos)
        {
            float c = RoomGrid.Cell;
            var lava = Cell(parent, "Lava", pos, new Vector3(c, 1f, c), false);
            Block(lava.transform, "Surface", Vector3.up * 0.012f, new Vector3(c, 0.02f, c), cfg.water, false, false, false);
            return lava;
        }

        /// <summary>자식 프롭을 부모 칸 가운데 바닥에, 가로세로 큰 쪽이 fit 이 되도록 다시 맞춘다 (프리팹 이전용)</summary>
        public static void RefitProp(Transform prop, float fit)
        {
            var parent = prop.parent;
            prop.localPosition = Vector3.zero;
            Bounds b = default;
            bool any = false;
            foreach (var r in prop.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!any) return;
            float s = Mathf.Min(1f, fit / Mathf.Max(b.size.x, b.size.z));
            prop.localScale *= s;
            var rel = parent.InverseTransformVector(b.center - parent.position) * s;
            float bottom = parent.InverseTransformVector(new Vector3(0f, b.min.y - parent.position.y, 0f)).y * s;
            prop.localPosition = -new Vector3(rel.x, bottom, rel.z);
        }

        /// <summary>
        /// 바닥: GameConfig.floorModel(맥스 Room_plan 메시)이 있으면 그걸, 없으면 사각 판.
        /// 텍스처는 판 전체에 한 장 (cfg.floor).
        /// </summary>
        public static GameObject BuildFloor(GameConfig cfg, Transform parent, Func<GameObject, Transform, GameObject> spawn)
        {
            GameObject floor;
            if (cfg.floorModel)
            {
                floor = spawn(cfg.floorModel, parent);
                floor.transform.localPosition = Vector3.zero;
            }
            else
            {
                floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
                floor.transform.SetParent(parent, false);
                floor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                floor.transform.localScale = new Vector3(FloorW, FloorL, 1f);
                Object.DestroyImmediate(floor.GetComponent<Collider>());
            }
            floor.name = "Floor";
            foreach (var fr in floor.GetComponentsInChildren<Renderer>())
            {
                fr.sharedMaterial = cfg.floor;
                fr.shadowCastingMode = ShadowCastingMode.Off;
            }
            return floor;
        }

        static Transform Group(Transform parent, string name)
        {
            var g = new GameObject(name).transform;
            g.SetParent(parent, false);
            return g;
        }

        /// <summary>바닥 중심이 pos 인 한 칸 판정 오브젝트 (박스 콜라이더, wall 이면 화살도 막음)</summary>
        static GameObject Cell(Transform parent, string name, Vector3 pos, Vector3 size, bool wall)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, size.y * 0.5f, 0f);
            box.size = size;
            if (wall) go.AddComponent<Wall>();
            return go;
        }

        /// <summary>
        /// 프롭을 놓는다. 가로·세로 중 큰 쪽이 fit 유닛이 되도록 균일 축소하고(작으면 그대로),
        /// 바닥 중심을 pos(부모 기준)에 맞춘다.
        /// </summary>
        static GameObject Prop(Transform parent, Func<GameObject, Transform, GameObject> spawn,
            GameObject prefab, Vector3 pos, float fit, float yaw)
        {
            var go = spawn(prefab, parent);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * go.transform.localRotation;
            go.transform.localPosition = Vector3.zero;
            Bounds b = default;
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!any) { go.transform.localPosition = pos; return go; }

            float s = Mathf.Min(1f, fit / Mathf.Max(b.size.x, b.size.z));
            go.transform.localScale *= s;
            var rel = parent.InverseTransformVector(b.center - parent.position) * s;
            float bottom = parent.InverseTransformVector(new Vector3(0f, b.min.y - parent.position.y, 0f)).y * s;
            go.transform.localPosition = pos - new Vector3(rel.x, bottom, rel.z);
            return go;
        }

        /// <summary>같은 배치도는 매번 같은 모양이 나오도록 하는 시드</summary>
        static int LayoutSeed(string[] rows)
        {
            unchecked
            {
                int h = 17;
                foreach (var row in rows)
                    foreach (char ch in row)
                        h = h * 31 + ch;
                return h;
            }
        }

        /// <summary>배치도(안쪽 16 x 26) 칸 중심</summary>
        public static Vector3 CellPos(int row, int col) => RoomGrid.InnerCenter(col, row);

        static GameObject Block(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat,
            bool collider, bool wall, bool shadows = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            if (!shadows) r.shadowCastingMode = ShadowCastingMode.Off;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            if (wall) go.AddComponent<Wall>();
            return go;
        }
    }
}

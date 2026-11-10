using System.Collections.Generic;
using UnityEngine;

namespace Archer
{
    /// <summary>
    /// 방 그리드 위의 칸 영역 (장애물 영역 · 구멍). 칸 목록만 저장하고,
    /// 판정은 붙어 있는 칸들을 가장 큰 직사각형으로 합친 박스 콜라이더 몇 개로 만든다 (칸마다 하나씩 만들지 않음).
    /// 콜라이더는 자식 "_Col" 로 생기고 프리팹에 같이 저장된다 (게임 중엔 다시 만들지 않음).
    /// </summary>
    [DisallowMultipleComponent]
    public class GridArea : MonoBehaviour
    {
        [Tooltip("화살·탄도 막는지 (장애물 영역 = 켬, 구멍 = 끔: 이동만 막고 화살은 통과)")]
        public bool blocksArrows = true;
        [Tooltip("판정 높이 (m)")]
        public float height = 1f;
        [HideInInspector] public List<Vector2Int> cells = new();

        public const string ColliderPrefix = "_Col";

        public bool Contains(Vector2Int c) => cells.Contains(c);

        public bool Add(Vector2Int c)
        {
            if (cells.Contains(c)) return false;
            cells.Add(c);
            return true;
        }

        public bool Remove(Vector2Int c) => cells.Remove(c);

        /// <summary>칸 목록대로 합친 콜라이더를 다시 만든다</summary>
        public void RebuildColliders()
        {
            var old = new List<GameObject>();
            foreach (Transform t in transform)
                if (t.name.StartsWith(ColliderPrefix)) old.Add(t.gameObject);
            foreach (var o in old)
            {
                if (Application.isPlaying) Destroy(o);
                else DestroyImmediate(o);
            }

            float c = RoomGrid.Cell;
            foreach (var r in Merge(cells))
            {
                var go = new GameObject($"{ColliderPrefix}_{r.width}x{r.height}");
                go.transform.SetParent(transform, false);
                // 직사각형 양 끝 칸 중심의 가운데
                var a = RoomGrid.Center(r.xMin, r.yMin);
                var b = RoomGrid.Center(r.xMax - 1, r.yMax - 1);
                go.transform.localPosition = (a + b) * 0.5f;
                var box = go.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, height * 0.5f, 0f);
                box.size = new Vector3(r.width * c, height, r.height * c);
                if (blocksArrows) go.AddComponent<Wall>();
            }
        }

        /// <summary>
        /// 칸들을 겹치지 않는 직사각형으로 욕심껏 합친다:
        /// 위→아래, 왼→오른쪽으로 훑으며 비어 있는 첫 칸에서 오른쪽으로 최대한 늘리고, 그 폭 그대로 아래로 최대한 늘린다.
        /// </summary>
        public static List<RectInt> Merge(IEnumerable<Vector2Int> source)
        {
            var set = new HashSet<Vector2Int>(source);
            var used = new HashSet<Vector2Int>();
            var list = new List<Vector2Int>(set);
            list.Sort((p, q) => p.y != q.y ? p.y.CompareTo(q.y) : p.x.CompareTo(q.x));
            var result = new List<RectInt>();
            foreach (var s in list)
            {
                if (used.Contains(s)) continue;
                int w = 1;
                while (Free(new Vector2Int(s.x + w, s.y))) w++;
                int h = 1;
                while (RowFree(s.x, s.y + h, w)) h++;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        used.Add(new Vector2Int(s.x + x, s.y + y));
                result.Add(new RectInt(s.x, s.y, w, h));
            }
            return result;

            bool Free(Vector2Int p) => set.Contains(p) && !used.Contains(p);
            bool RowFree(int x0, int y, int w)
            {
                for (int x = 0; x < w; x++)
                    if (!Free(new Vector2Int(x0 + x, y))) return false;
                return true;
            }
        }
    }
}

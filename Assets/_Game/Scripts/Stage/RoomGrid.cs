using UnityEngine;

namespace Archer
{
    /// <summary>
    /// 방 그리드 규격. 바닥 메시 Room_plan(13.5 x 21m)과 같은 18 x 28칸, 한 칸 0.75m.
    /// 가장 바깥 한 줄은 벽 자리라 장애물·적을 놓지 않는다 → 배치 가능한 칸은 안쪽 16 x 26.
    /// 좌표계: 방 중심이 원점, col 은 왼→오, row 는 위(문 쪽)→아래.
    /// </summary>
    public static class RoomGrid
    {
        public const float Cell = 0.75f;
        public const int Cols = 18;
        public const int Rows = 28;
        public const int InnerCols = Cols - 2; // 16
        public const int InnerRows = Rows - 2; // 26

        public const float Width = Cols * Cell;   // 13.5
        public const float Length = Rows * Cell;  // 21
        /// <summary>안쪽(플레이 영역) 반폭/반길이</summary>
        public const float InnerHalfW = InnerCols * Cell * 0.5f; // 6
        public const float InnerHalfL = InnerRows * Cell * 0.5f; // 9.75

        /// <summary>전체 그리드(0..17, 0..27) 칸 중심</summary>
        public static Vector3 Center(int col, int row) =>
            new((col - (Cols - 1) * 0.5f) * Cell, 0f, ((Rows - 1) * 0.5f - row) * Cell);

        /// <summary>안쪽 그리드(0..15, 0..25) 칸 중심. 배치도 문자열은 이 기준.</summary>
        public static Vector3 InnerCenter(int col, int row) => Center(col + 1, row + 1);

        /// <summary>위치 → 전체 그리드 칸 (범위 밖이면 바깥값 그대로)</summary>
        public static Vector2Int ToCell(Vector3 p) => new(
            Mathf.RoundToInt(p.x / Cell + (Cols - 1) * 0.5f),
            Mathf.RoundToInt((Rows - 1) * 0.5f - p.z / Cell));

        public static bool IsInner(Vector2Int c) =>
            c.x >= 1 && c.x <= Cols - 2 && c.y >= 1 && c.y <= Rows - 2;

        /// <summary>장애물 기본 크기 (2 x 2칸 = 1.5m)</summary>
        public const int ObstacleCells = 2;

        /// <summary>
        /// n x n칸 블록의 중심을 그리드에 맞추고 블록 전체가 안쪽 영역에 들어오게 제한한다.
        /// n 이 짝수면 중심이 칸 경계(격자점), 홀수면 칸 중심에 온다.
        /// </summary>
        public static Vector3 SnapBlock(Vector3 p, int n)
        {
            if (n <= 1) return SnapInner(p);
            float off = (n % 2 == 0) ? 0f : Cell * 0.5f;
            float x = Mathf.Round((p.x - off) / Cell) * Cell + off;
            float z = Mathf.Round((p.z - off) / Cell) * Cell + off;
            float mx = InnerHalfW - n * Cell * 0.5f, mz = InnerHalfL - n * Cell * 0.5f;
            return new Vector3(Mathf.Clamp(x, -mx, mx), p.y, Mathf.Clamp(z, -mz, mz));
        }

        /// <summary>블록이 덮는 전체 그리드 칸들</summary>
        public static System.Collections.Generic.IEnumerable<Vector2Int> BlockCells(Vector3 center, int n)
        {
            var first = ToCell(center + new Vector3(-(n - 1) * Cell * 0.5f, 0f, (n - 1) * Cell * 0.5f));
            for (int dx = 0; dx < n; dx++)
                for (int dy = 0; dy < n; dy++)
                    yield return new Vector2Int(first.x + dx, first.y + dy);
        }

        /// <summary>가장 가까운 안쪽 칸 중심으로 맞춘다 (y 는 유지)</summary>
        public static Vector3 SnapInner(Vector3 p)
        {
            var c = ToCell(p);
            c.x = Mathf.Clamp(c.x, 1, Cols - 2);
            c.y = Mathf.Clamp(c.y, 1, Rows - 2);
            var s = Center(c.x, c.y);
            s.y = p.y;
            return s;
        }
    }
}

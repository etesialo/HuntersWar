using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Archer.EditorTools
{
    /// <summary>
    /// 방 프리팹 편집 중 자동 칸 맞춤.
    /// Obstacles / Holes / Spawns 아래 오브젝트를 옮기면 그리드(0.75m)에 붙이고,
    /// 바깥 한 줄(벽 자리)이나 방 밖이면 안쪽으로 되돌린다. 구멍을 옮기면 구멍 모양을 다시 만든다.
    /// </summary>
    [InitializeOnLoad]
    public static class RoomGridTools
    {
        static bool busy;

        static RoomGridTools()
        {
            ObjectChangeEvents.changesPublished += OnChanges;
        }

        static void OnChanges(ref ObjectChangeEventStream stream)
        {
            if (busy || EditorApplication.isPlayingOrWillChangePlaymode) return;
            for (int i = 0; i < stream.length; i++)
            {
                if (stream.GetEventType(i) != ObjectChangeKind.ChangeGameObjectOrComponentProperties) continue;
                stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out var e);
#pragma warning disable CS0618
                var obj = EditorUtility.InstanceIDToObject(e.instanceId);
#pragma warning restore CS0618
                var go = obj as GameObject ?? (obj as Component)?.gameObject;
                if (go) Snap(go.transform);
            }
        }

        static void Snap(Transform t)
        {
            var group = t.parent;
            if (group == null || (group.name != "Obstacles" && group.name != "Spawns" && group.name != "Holes")) return;
            if (t.name.StartsWith("_")) return; // 굽기용 단면 등 자동 생성물은 칸에 맞추지 않음
            var room = group.GetComponentInParent<RoomRoot>();
            if (room == null) return;

            var local = room.transform.InverseTransformPoint(t.position);
            // 장애물은 콜라이더 크기로 칸 수를 판단 (기본 2 x 2), 구멍·스폰은 1칸
            int n = 1;
            if (group.name == "Obstacles" && t.TryGetComponent<BoxCollider>(out var box))
                n = Mathf.Max(1, Mathf.RoundToInt(box.size.x / RoomGrid.Cell));
            var snapped = RoomGrid.SnapBlock(local, n);
            snapped.y = local.y;

            if ((snapped - local).sqrMagnitude > 1e-6f)
            {
                bool outside = RoomGrid.BlockCells(local, n).Any(c => !RoomGrid.IsInner(c));
                if (outside && (snapped - local).sqrMagnitude > RoomGrid.Cell * RoomGrid.Cell * 0.25f)
                    Debug.LogWarning($"[Archer] '{t.name}': 바깥 한 줄(벽 자리)이나 방 밖에는 놓을 수 없어서 안쪽으로 옮겼어요.", t);
                busy = true;
                try
                {
                    Undo.RecordObject(t, "Snap to room grid");
                    t.position = room.transform.TransformPoint(snapped);
                }
                finally { busy = false; }
            }

            if (group.name == "Holes" && group.TryGetComponent<FloorHoles>(out var holes)) holes.Rebuild();
            if (group.name == "Obstacles" && room.TryGetComponent<RoomFloorAO>(out var ao)) ao.Rebuild();
        }
    }
}

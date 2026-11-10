using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Archer
{
    /// <summary>
    /// 보이지 않는 조이스틱. 화면 어디든 누른 곳이 중심이 되고, 끌어당긴 방향으로 움직인다.
    /// 그림은 없다. 버튼 같은 UI 를 누른 경우만 무시한다.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour
    {
        [Tooltip("끝까지 끌었다고 보는 거리 (1920 높이 화면 기준 픽셀)")]
        public float radius = 140f;
        [Range(0f, 0.5f)] public float deadZone = 0.12f;

        public Vector2 Direction { get; private set; }

        bool active;
        Vector2 origin;
        static readonly List<RaycastResult> hits = new();

        void OnDisable() => Release();

        void Update()
        {
            var pointer = Pointer.current;
            if (pointer == null) return;
            var pos = pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame && !active && !OverButton(pos))
            {
                active = true;
                origin = pos;
            }
            if (!active) return;
            if (!pointer.press.isPressed) { Release(); return; }

            float r = radius * Screen.height / 1920f;
            var v = Vector2.ClampMagnitude(pos - origin, r);
            // 궁수의 전설처럼 기울이면 항상 최고 속도
            Direction = v.magnitude < r * deadZone ? Vector2.zero : v.normalized;
        }

        /// <summary>버튼·토글 등 누를 수 있는 UI 위인지</summary>
        static bool OverButton(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            hits.Clear();
            es.RaycastAll(new PointerEventData(es) { position = screenPos }, hits);
            foreach (var h in hits)
                if (h.gameObject.GetComponentInParent<UnityEngine.UI.Selectable>() != null) return true;
            return false;
        }

        void Release()
        {
            active = false;
            Direction = Vector2.zero;
        }
    }
}

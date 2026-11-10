using UnityEngine;
using UnityEngine.UI;

namespace Archer
{
    /// <summary>캐릭터 머리 위를 따라다니는 체력바 (스크린 UI).</summary>
    [DefaultExecutionOrder(100)]
    public class HpBar : MonoBehaviour
    {
        public RectTransform fill;
        public Image fillImage;
        public Text label;

        Transform target;
        float offset;
        UIManager ui;
        RectTransform rt;

        public void Bind(UIManager owner, Transform follow, float heightOffset, float width, Color color, bool showLabel)
        {
            ui = owner;
            target = follow;
            offset = heightOffset;
            rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(width, rt.sizeDelta.y);
            fillImage.color = color;
            label.gameObject.SetActive(showLabel);
            gameObject.SetActive(true);
            Set(1f);
            Follow();
        }

        public void Set(float ratio, string text = null)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
            if (text != null) label.text = text;
        }

        void LateUpdate() => Follow();

        void Follow()
        {
            if (target == null)
            {
                Destroy(gameObject);
                return;
            }
            if (ui != null && ui.WorldToLocal(target.position + Vector3.up * offset, out var lp))
                rt.anchoredPosition = lp;
        }
    }
}

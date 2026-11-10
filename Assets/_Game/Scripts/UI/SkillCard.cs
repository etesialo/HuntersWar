using UnityEngine;
using UnityEngine.UI;

namespace Archer
{
    public class SkillCard : MonoBehaviour
    {
        public Button button;
        public Image icon;
        public Text iconText;
        public Text nameText;
        public Text descText;
        public Text levelText;
        [Tooltip("카드 안쪽 색 (스킬 종류 색으로 물듦). 비면 안 물들임")]
        public Image tint;

        public void Set(SkillDef def, int level, System.Action onClick, UiSkin skin = null)
        {
            var sprite = skin ? skin.Icon(def.id) : null;
            if (sprite)
            {
                // 그림 아이콘: 색 덧칠 없이, 글자 숨김
                icon.sprite = sprite;
                icon.type = Image.Type.Simple;
                icon.preserveAspect = true;
                icon.color = Color.white;
                iconText.text = "";
            }
            else
            {
                icon.color = def.color;
                iconText.text = def.icon;
            }
            if (tint)
            {
                var c = def.color;
                // 스킬 종류 색을 어둡게 깔아 글자가 잘 읽히게 (밝으면 카드가 바래 보임)
                tint.color = new Color(c.r * 0.38f, c.g * 0.38f, c.b * 0.38f, 0.85f);
            }
            nameText.text = def.name;
            descText.text = def.desc;
            levelText.text = def.maxLevel >= 99 ? "" : level == 0 ? "NEW!" : $"Lv.{level} → Lv.{level + 1}";
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick());
        }
    }
}

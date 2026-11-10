using UnityEngine;

namespace Archer
{
    /// <summary>
    /// UI 그림 묶음 (네이비·골드 테마). 에디터 메뉴 "Archer/UI 스킨 적용" 이 InGame 씬 UI 에 입히고,
    /// 게임 중엔 UIManager 가 스킬 아이콘 / 스테이지 진행 점에 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "Archer/UI Skin", fileName = "UiSkin")]
    public class UiSkin : ScriptableObject
    {
        [Header("틀")]
        public Sprite panel;
        public Sprite card;
        public Sprite banner;
        public Sprite buttonGold;
        public Sprite buttonNavy;
        public Sprite pill;

        [Header("바")]
        public Sprite barFrame;
        public Sprite barFill;
        public Color expColor = new(0.3f, 0.85f, 0.95f);
        public Color bossColor = new(0.95f, 0.25f, 0.25f);

        [Header("HUD")]
        public Sprite levelBadge;
        public Sprite coin;
        public Sprite pauseIcon;
        public Sprite iconRing;

        [Header("스테이지 진행 점")]
        public Sprite nodeDone;
        public Sprite nodeCurrent;
        public Sprite nodeBoss;
        public Color nodeUpcoming = new(0.35f, 0.38f, 0.5f, 0.9f);

        [Header("스킬 아이콘 (SkillId 순서)")]
        public Sprite[] skillIcons;

        [Header("글자")]
        [Tooltip("UI 전체 글꼴 (Jua, OFL 라이선스 — Art/Fonts/Jua-OFL.txt)")]
        public Font font;
        public Color gold = new(1f, 0.84f, 0.38f);
        public Color textDark = new(0.28f, 0.1f, 0f);

        public Sprite Icon(SkillId id)
        {
            int i = (int)id;
            return skillIcons != null && i >= 0 && i < skillIcons.Length ? skillIcons[i] : null;
        }
    }
}

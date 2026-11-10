using System.Collections.Generic;
using UnityEngine;

namespace Archer
{
    public enum SkillId
    {
        Multishot, FrontArrow, DiagonalArrow, SideArrow, RearArrow,
        Pierce, Ricochet, BouncyWall,
        AttackBoost, AttackSpeed, CritMaster,
        HpBoost, Heal, Bloodthirst,
        FireArrow, PoisonArrow, FrostArrow,
        FireOrb, Agility, IronSkin,
        // 새 항목은 항상 맨 뒤에 (UiSkin.skillIcons 가 이 순서 번호로 아이콘을 찾음)
        LightningArrow, WindArrow, RockArrow,
    }

    public class SkillDef
    {
        public SkillId id;
        public string name;
        public string desc;
        public string icon;
        public int maxLevel;
        public Color color;
        public float weight;
    }

    public static class SkillDatabase
    {
        static readonly Color Arrow = new(0.95f, 0.75f, 0.25f);
        static readonly Color Stat = new(0.9f, 0.35f, 0.35f);
        static readonly Color Life = new(0.35f, 0.8f, 0.4f);
        static readonly Color Element = new(0.55f, 0.45f, 0.95f);
        static readonly Color Etc = new(0.3f, 0.7f, 0.95f);

        public static readonly SkillDef[] All =
        {
            D(SkillId.Multishot,     "멀티샷",       "화살을 한 번 더 연속으로 발사\n(화살 피해 -10%)", "멀", 3, Arrow, 1.2f),
            D(SkillId.FrontArrow,    "전방 화살 +1", "정면으로 화살을 하나 더 나란히 발사\n(화살 피해 -15%)", "전", 3, Arrow, 1.2f),
            D(SkillId.DiagonalArrow, "대각선 화살",  "대각선 양쪽으로 화살 2발 추가", "대", 2, Arrow, 1f),
            D(SkillId.SideArrow,     "측면 화살",    "양옆으로 화살 2발 추가", "측", 1, Arrow, 0.8f),
            D(SkillId.RearArrow,     "후방 화살",    "뒤쪽으로 화살 1발 추가", "후", 2, Arrow, 0.8f),
            D(SkillId.Pierce,        "관통",         "화살이 적을 꿰뚫고 지나감\n(관통할 때마다 피해 -33%)", "관", 2, Arrow, 1f),
            D(SkillId.Ricochet,      "도탄",         "적에게 맞은 화살이 주변 적에게 2번 튕김", "도", 2, Arrow, 1f),
            D(SkillId.BouncyWall,    "벽 반사",      "화살이 벽에 2번 튕겨 나옴", "벽", 1, Arrow, 0.8f),
            D(SkillId.AttackBoost,   "공격력 증가",  "공격력 +25%", "공", 5, Stat, 1.3f),
            D(SkillId.AttackSpeed,   "공격 속도 증가", "공격 속도 +20%", "속", 5, Stat, 1.3f),
            D(SkillId.CritMaster,    "치명타 마스터", "치명타 확률 +10%\n치명타 피해 +40%", "치", 3, Stat, 1f),
            D(SkillId.HpBoost,       "체력 증가",    "최대 체력 +20%\n늘어난 만큼 회복", "체", 5, Life, 1f),
            D(SkillId.Heal,          "회복",         "체력을 40% 회복", "회", 99, Life, 0.7f),
            D(SkillId.Bloodthirst,   "흡혈",         "적을 처치하면 최대 체력의 2% 회복", "흡", 3, Life, 0.9f),
            D(SkillId.FireArrow,     "불꽃 화살",    "맞은 적에게 화상\n2초 동안 지속 피해", "불", 1, Element, 0.9f),
            D(SkillId.PoisonArrow,   "독 화살",      "맞은 적을 중독시킴\n맞을수록 독이 누적", "독", 1, Element, 0.9f),
            D(SkillId.FrostArrow,    "얼음 화살",    "맞은 적이 느려지고\n20% 확률로 빙결", "얼", 1, Element, 0.9f),
            D(SkillId.LightningArrow,"번개 화살",    "맞은 적 주변의 적 2명에게\n번개가 튀어 피해의 40%", "번", 1, Element, 0.9f),
            D(SkillId.WindArrow,     "바람 화살",    "화살이 40% 더 빠르게 날아가고\n적을 멀리 밀쳐냄", "풍", 1, Element, 0.9f),
            D(SkillId.RockArrow,     "바위 화살",    "맞은 적을 25% 확률로\n0.8초 기절시킴", "암", 1, Element, 0.9f),
            D(SkillId.FireOrb,      "화염 구체",    "주위를 도는 화염 구체 2개 소환", "구", 2, Element, 0.8f),
            D(SkillId.Agility,       "신속",         "이동 속도 +15%", "신", 2, Etc, 0.7f),
            D(SkillId.IronSkin,      "단단한 몸",    "받는 피해 -15%", "방", 3, Etc, 0.8f),
        };

        static SkillDef D(SkillId id, string name, string desc, string icon, int max, Color color, float weight) =>
            new() { id = id, name = name, desc = desc, icon = icon, maxLevel = max, color = color, weight = weight };

        public static SkillDef Get(SkillId id)
        {
            foreach (var d in All)
                if (d.id == id) return d;
            return null;
        }

        /// <summary>최대 레벨이 아닌 스킬 중에서 가중치로 count개를 중복 없이 뽑는다.</summary>
        public static List<SkillDef> Roll(Dictionary<SkillId, int> levels, int count, PlayerController player)
        {
            var pool = new List<SkillDef>();
            foreach (var d in All)
            {
                levels.TryGetValue(d.id, out int lv);
                if (lv >= d.maxLevel) continue;
                if (d.id == SkillId.Heal && player.Hp >= player.MaxHp * 0.95f) continue;
                pool.Add(d);
            }

            var result = new List<SkillDef>();
            while (result.Count < count && pool.Count > 0)
            {
                float total = 0f;
                foreach (var d in pool) total += d.weight;
                float r = Random.value * total;
                int pick = pool.Count - 1;
                for (int i = 0; i < pool.Count; i++)
                {
                    r -= pool[i].weight;
                    if (r <= 0f) { pick = i; break; }
                }
                result.Add(pool[pick]);
                pool.RemoveAt(pick);
            }
            return result;
        }
    }
}

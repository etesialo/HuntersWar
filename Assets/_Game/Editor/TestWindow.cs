using UnityEditor;
using UnityEngine;

namespace Archer.EditorTools
{
    /// <summary>
    /// 플레이 중 테스트용 별도 창 (메뉴: Archer/테스트 패널).
    /// 스킬 레벨 올리기/내리기, 무적, 스킬 초기화. 게임 화면 위에 겹치지 않는다.
    /// </summary>
    public class TestWindow : EditorWindow
    {
        [MenuItem("Archer/테스트 패널")]
        static void Open() => GetWindow<TestWindow>("테스트 패널").minSize = new Vector2(360f, 400f);

        static readonly (string title, SkillId[] ids)[] Groups =
        {
            ("화살", new[] { SkillId.Multishot, SkillId.FrontArrow, SkillId.DiagonalArrow, SkillId.SideArrow, SkillId.RearArrow, SkillId.Pierce, SkillId.Ricochet, SkillId.BouncyWall }),
            ("속성", new[] { SkillId.FireArrow, SkillId.FrostArrow, SkillId.PoisonArrow, SkillId.LightningArrow, SkillId.WindArrow, SkillId.RockArrow, SkillId.FireOrb }),
            ("능력치", new[] { SkillId.AttackBoost, SkillId.AttackSpeed, SkillId.CritMaster, SkillId.Agility }),
            ("생명", new[] { SkillId.HpBoost, SkillId.Heal, SkillId.Bloodthirst, SkillId.IronSkin }),
        };

        Vector2 scroll;
        GUIStyle big, nameStyle, head, lv;

        void OnInspectorUpdate()
        {
            if (EditorApplication.isPlaying) Repaint();
        }

        void Styles()
        {
            if (big != null) return;
            big = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold, fixedHeight = 36 };
            nameStyle = new GUIStyle(EditorStyles.label) { fontSize = 16, alignment = TextAnchor.MiddleLeft, fixedHeight = 40 };
            head = new GUIStyle(EditorStyles.boldLabel) { fontSize = 17 };
            lv = new GUIStyle(EditorStyles.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter, fixedHeight = 40, fontStyle = FontStyle.Bold };
        }

        void OnGUI()
        {
            Styles();
            var gm = EditorApplication.isPlaying ? GameManager.I : null;
            var p = gm ? gm.Player : null;
            if (!p)
            {
                EditorGUILayout.Space(20);
                EditorGUILayout.LabelField("플레이 중에만 쓸 수 있어요.", head);
                return;
            }

            // ── 상태 / 공통 ──
            EditorGUILayout.Space(6);
            var stage = gm.stage;
            EditorGUILayout.LabelField(
                $"Lv.{p.Level}   체력 {Mathf.CeilToInt(p.Hp)} / {Mathf.CeilToInt(p.MaxHp)}   웨이브 {(stage ? stage.Wave + 1 : 0)}   적 {(stage ? stage.Enemies.Count : 0)}", head);
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                var old = GUI.backgroundColor;
                GUI.backgroundColor = p.GodMode ? new Color(1f, 0.6f, 0.2f) : Color.gray;
                if (GUILayout.Button(p.GodMode ? "무적 ON" : "무적 OFF", big)) p.GodMode = !p.GodMode;
                GUI.backgroundColor = old;
                if (GUILayout.Button("스킬 초기화", big))
                    foreach (var d in SkillDatabase.All) p.SetSkillLevel(d.id, 0);
                if (GUILayout.Button("체력 회복", big)) p.Heal(p.MaxHp);
            }
            EditorGUILayout.Space(8);

            var skin = gm.ui ? gm.ui.skin : null;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var (title, ids) in Groups)
            {
                EditorGUILayout.LabelField(title, head);
                foreach (var id in ids) Row(p, SkillDatabase.Get(id), skin);
                EditorGUILayout.Space(10);
            }
            EditorGUILayout.EndScrollView();
        }

        void Row(PlayerController p, SkillDef d, UiSkin skin)
        {
            p.Skills.TryGetValue(d.id, out int level);
            using (new EditorGUILayout.HorizontalScope(GUILayout.Height(42)))
            {
                var icon = skin ? skin.Icon(d.id) : null;
                var r = GUILayoutUtility.GetRect(40, 40, GUILayout.Width(40), GUILayout.Height(40));
                if (icon) GUI.DrawTextureWithTexCoords(r, icon.texture, TexRect(icon));

                var old = GUI.contentColor;
                if (level > 0) GUI.contentColor = new Color(1f, 0.8f, 0.35f);
                GUILayout.Label(d.name, nameStyle, GUILayout.MinWidth(120));
                GUI.contentColor = old;

                GUILayout.Label($"{level} / {d.maxLevel}", lv, GUILayout.Width(70));
                using (new EditorGUI.DisabledScope(level <= 0))
                    if (GUILayout.Button("−", big, GUILayout.Width(48))) p.SetSkillLevel(d.id, level - 1);
                using (new EditorGUI.DisabledScope(level >= d.maxLevel))
                    if (GUILayout.Button("+", big, GUILayout.Width(48)))
                    {
                        if (d.id == SkillId.Heal) p.ApplySkill(d.id); // 회복은 누르면 바로 회복
                        else p.SetSkillLevel(d.id, level + 1);
                    }
            }
        }

        static Rect TexRect(Sprite s)
        {
            var t = s.texture;
            var r = s.textureRect;
            return new Rect(r.x / t.width, r.y / t.height, r.width / t.width, r.height / t.height);
        }
    }
}

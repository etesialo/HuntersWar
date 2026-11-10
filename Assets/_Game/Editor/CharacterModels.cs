using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Archer.EditorTools
{
    /// <summary>
    /// "1. 전체 세팅"에서 호출. 기본 도형으로 만든 플레이어/적 프리팹의 Model 안을
    /// WAF 캐릭터 모델로 바꾸고, 캐릭터마다 idle/move/attack/die 애니메이터를 만든다.
    /// 판정(CharacterController, 콜라이더, 수치)은 그대로 둔다.
    /// </summary>
    public static class CharacterModels
    {
        const string CharRoot = "Assets/_Game/Art/WAF/02.ProductData/Characters/";
        const string ControllerDir = "Assets/_Game/Art/Controllers";
        const string PrefabDir = "Assets/_Game/Prefabs";

        /// <summary>장애물 한 개(2 x 2칸) 폭</summary>
        const float Block = RoomGrid.Cell * RoomGrid.ObstacleCells; // 1.5m

        /// <summary>
        /// (우리 프리팹, WAF 캐릭터 폴더, 목표 바닥 크기(가로·세로 중 큰 쪽, m), 모델 올릴 높이).
        /// 일반 캐릭터는 장애물 2 x 2칸과 비슷한 크기, 보스는 더 크게.
        /// </summary>
        static readonly (string prefab, string character, float footprint, float lift)[] Map =
        {
            ("Player", "Player/chr_betty1", Block * 1.6f, 0f),
            ("Slime", "Monster/mob_appleslime1", Block, 0f),
            ("Bat", "Monster/mob_eagle1", Block, 0.5f),
            ("SkeletonArcher", "Monster/mob_froggy_magic1", Block, 0f),
            ("Boar", "Monster/mob_wildboar1", Block, 0f),
            ("Plant", "Monster/mob_lickingplant1", Block, 0f),
            ("KingSlime", "Monster/mob_waterslime1", Block * 2f, 0f),   // 4 x 4칸
            ("DarkKnight", "Monster/boss_giantsheep1", Block * 3f, 0f), // 6 x 6칸
        };

        public static void ApplyAll()
        {
            if (!AssetDatabase.IsValidFolder(ControllerDir))
            {
                Directory.CreateDirectory(ControllerDir);
                AssetDatabase.Refresh();
            }
            foreach (var m in Map)
            {
                try { Apply(m.prefab, m.character, m.footprint, m.lift); }
                catch (System.Exception e) { Debug.LogError($"[Archer] {m.prefab} 모델 적용 실패: {e}"); }
            }
            AssetDatabase.SaveAssets();
        }

        static void Apply(string prefabName, string character, float footprint, float lift)
        {
            string id = Path.GetFileName(character);
            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CharRoot}{character}/Models/{id}.prefab");
            if (modelPrefab == null) { Debug.LogWarning($"[Archer] 모델 없음: {character}"); return; }
            var controller = BuildController(id, $"{CharRoot}{character}/Anim");

            string path = $"{PrefabDir}/{prefabName}.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var model = root.transform.Find("Model");
                if (model == null) { Debug.LogWarning($"[Archer] {prefabName} 에 Model 없음"); return; }
                for (int i = model.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(model.GetChild(i).gameObject);

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, model);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                // 사라진 스크립트(예전 프로젝트 전용)가 있으면 프리팹 저장이 막히므로 제거
                foreach (var t in inst.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

                var animator = inst.GetComponentInChildren<Animator>();
                if (animator == null) animator = inst.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (!inst.GetComponent<CharacterAnim>()) inst.AddComponent<CharacterAnim>();
                // 예전 프로젝트 모델은 그림자 생성이 꺼져 있음 → 바닥에 그림자가 맺히게 켠다
                // 캐릭터 레이어도 붙여서 디렉셔널(ShadowOnlyLight)이 캐릭터 그림자만 만들게
                foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    smr.renderingLayerMask |= ShadowOnlyLight.CharacterMask;
                }

                // 바닥 크기(가로·세로 중 큰 쪽)를 목표에 맞추고 바닥에 세운다
                // (크기는 배율 1에서 한 번만 재고 계산한다. 굽기 측정이 부모 배율을 반영하지 않아서)
                var b = RendererBounds(inst);
                var pivot = inst.transform.position;
                float wide = Mathf.Max(b.size.x, b.size.z);
                float s = wide > 0.001f ? footprint / wide : 1f;
                inst.transform.localScale = Vector3.one * s;
                float minY = pivot.y + (b.min.y - pivot.y) * s;
                float maxY = pivot.y + (b.max.y - pivot.y) * s;
                inst.transform.position += new Vector3(0f, model.position.y + lift - minY, 0f);
                // 체력바는 머리 위로
                float barY = maxY - minY + lift + 0.35f;
                var enemy = root.GetComponent<Enemy>();
                if (enemy) enemy.hpBarOffset = barY;
                var pc = root.GetComponent<PlayerController>();
                if (pc) pc.hpBarOffset = barY;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[Archer] {prefabName} ← {id}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static Bounds RendererBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>()
                .Where(r => r is SkinnedMeshRenderer || r is MeshRenderer)
                .Where(r => !r.name.ToLower().Contains("shadow")).ToArray();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            // 스킨 메시는 bounds 에 여유가 커서 실제 정점으로 잰다
            bool any = false;
            Bounds b = default;
            void Add(Vector3 w)
            {
                if (!any) { b = new Bounds(w, Vector3.zero); any = true; }
                else b.Encapsulate(w);
            }
            foreach (var r in rs)
            {
                if (r is SkinnedMeshRenderer smr && smr.sharedMesh)
                {
                    // 스킨 메시는 bounds 여유가 커서 실제 정점으로 잰다.
                    // (본이 최적화로 숨겨진 리그라 BakeMesh 사용: 스케일 포함으로 굽고 위치·회전만 적용)
                    var baked = new Mesh();
                    smr.BakeMesh(baked, true);
                    var m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                    foreach (var v in baked.vertices) Add(m.MultiplyPoint3x4(v));
                    Object.DestroyImmediate(baked);
                }
                else if (r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh)
                {
                    var m = r.transform.localToWorldMatrix;
                    foreach (var v in mf.sharedMesh.vertices) Add(m.MultiplyPoint3x4(v));
                }
            }
            return any ? b : new Bounds(go.transform.position, Vector3.zero);
        }

        /// <summary>idle / move / attack / die 상태만 있는 컨트롤러. 전환은 CharacterAnim 이 코드로 한다.</summary>
        static AnimatorController BuildController(string id, string animDir)
        {
            string path = $"{ControllerDir}/{id}.controller";
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path)
                       ?? AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = ctrl.layers[0].stateMachine;
            foreach (var st in sm.states.ToArray()) sm.RemoveState(st.state);

            AddState(sm, "idle", Clip(animDir, "idle_01"), true);
            AddState(sm, "move", Clip(animDir, "move_01"), true);
            AddState(sm, "attack", Clip(animDir, "attack_01") ?? Clip(animDir, "skill_01"), false);
            AddState(sm, "die", Clip(animDir, "die_01"), false);
            sm.defaultState = sm.states.First(s => s.state.name == "idle").state;
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        static AnimationClip Clip(string dir, string name) =>
            AssetDatabase.LoadAssetAtPath<AnimationClip>($"{dir}/{name}.anim");

        static void AddState(AnimatorStateMachine sm, string name, AnimationClip clip, bool loop)
        {
            var st = sm.AddState(name);
            st.motion = clip;
            if (clip == null) return;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (settings.loopTime != loop)
            {
                settings.loopTime = loop;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip);
            }
        }
    }
}

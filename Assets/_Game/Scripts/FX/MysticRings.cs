using UnityEngine;

namespace Archer
{
    /// <summary>
    /// 영험한 빛의 링 층: 둥근 링 여러 겹이 가운데에서 생겨나 퍼지며 올라가 사라진다.
    /// 링 개수만큼 쿼드를 만들고, 움직임은 셰이더(Archer/MysticGlow)가 한다.
    /// 링은 오브젝트 XZ 평면에 누워서 +Y 로 올라간다 → 오브젝트를 돌리면 그 방향으로 뻗어 나감.
    /// 모든 값은 오브젝트마다 따로 (MaterialPropertyBlock).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class MysticRings : MonoBehaviour
    {
        [Header("링")]
        [Tooltip("동시에 보이는 링 겹 수")]
        [Range(1, 12)] public int layers = 4;
        [ColorUsage(false, true)] public Color color = new Color(0.35f, 1f, 0.8f);
        [Min(0f)] public float intensity = 1f;
        [Tooltip("초당 링 생성 주기")]
        [Min(0f)] public float speed = 0.35f;
        [Tooltip("생겨날 때 지름 (오브젝트 단위)")]
        [Min(0f)] public float startSize = 0.2f;
        [Tooltip("사라질 때 지름")]
        [Min(0.01f)] public float endSize = 1.6f;
        [Tooltip("올라가는 높이")]
        [Min(0f)] public float rise = 0.8f;
        [Tooltip("1 = 고르게, 작을수록 처음에 빨리 올라감")]
        [Range(0.3f, 3f)] public float riseCurve = 0.7f;
        [Range(0.01f, 0.5f)] public float ringWidth = 0.08f;
        [Tooltip("0 = 또렷, 1 = 흐릿")]
        [Range(0f, 1f)] public float ringSoftness = 0.8f;
        [Tooltip("링 안쪽 은은하게 채움")]
        [Range(0f, 1f)] public float innerFill = 0.15f;
        [Tooltip("생겨날 때 서서히 나타나는 구간 (0~1)")]
        [Range(0.01f, 1f)] public float fadeIn = 0.15f;
        [Tooltip("퍼지며 사라지는 곡선 (클수록 빨리 사라짐)")]
        [Range(0.2f, 6f)] public float fadeOut = 1.5f;

        [Header("가운데 빛")]
        public bool core = true;
        [ColorUsage(false, true)] public Color coreColor = new Color(0.7f, 1f, 0.9f);
        [Range(0f, 2f)] public float coreStrength = 0.4f;
        [Tooltip("가운데 빛 지름")]
        [Min(0.01f)] public float coreSize = 0.6f;
        [Tooltip("숨 쉬는 정도")]
        [Range(0f, 1f)] public float pulse = 0.3f;

        [Tooltip("벽에 묻히지 않게 화면 위치는 그대로 두고 카메라 쪽으로 당김 (m)")]
        [Range(0f, 3f)] public float depthPull = 0.3f;

        Mesh mesh;
        MaterialPropertyBlock block;
        int builtLayers = -1;
        bool builtCore;
        bool dirty;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int SpeedId = Shader.PropertyToID("_Speed");
        static readonly int StartSizeId = Shader.PropertyToID("_StartSize");
        static readonly int EndSizeId = Shader.PropertyToID("_EndSize");
        static readonly int RiseId = Shader.PropertyToID("_Rise");
        static readonly int RiseCurveId = Shader.PropertyToID("_RiseCurve");
        static readonly int RingWidthId = Shader.PropertyToID("_RingWidth");
        static readonly int RingSoftId = Shader.PropertyToID("_RingSoft");
        static readonly int InnerFillId = Shader.PropertyToID("_InnerFill");
        static readonly int FadeInId = Shader.PropertyToID("_FadeIn");
        static readonly int FadeOutId = Shader.PropertyToID("_FadeOut");
        static readonly int CoreStrengthId = Shader.PropertyToID("_CoreStrength");
        static readonly int CoreSizeId = Shader.PropertyToID("_CoreSize");
        static readonly int PulseId = Shader.PropertyToID("_Pulse");
        static readonly int DepthPullId = Shader.PropertyToID("_DepthPull");

        void OnEnable() => Refresh();
        // OnValidate 안에서 메시를 바꾸면 경고가 나서, 다음 Update 에서 반영
        void OnValidate() => dirty = true;

        void Update()
        {
            if (!dirty) return;
            dirty = false;
            Refresh();
        }

        void OnDestroy()
        {
            if (mesh) DestroyImmediate(mesh);
        }

        void Refresh()
        {
            if (!mesh || builtLayers != layers || builtCore != core) BuildMesh();
            // 셰이더가 키우고 올리니 화면 밖 판정 범위를 값에 맞춤
            float wide = Mathf.Max(endSize, startSize, core ? coreSize : 0f);
            mesh.bounds = new Bounds(new Vector3(0f, rise * 0.5f, 0f), new Vector3(wide, rise + 0.1f, wide));
            Apply();
        }

        void BuildMesh()
        {
            if (!mesh) mesh = new Mesh { name = "MysticRings", hideFlags = HideFlags.DontSave };
            mesh.Clear();
            builtLayers = layers;
            builtCore = core;

            int quads = layers + (core ? 1 : 0);
            var pos = new Vector3[quads * 4];
            var corner = new Vector2[quads * 4];
            var layer = new Vector2[quads * 4];
            var tris = new int[quads * 6];
            var corners = new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(0.5f, 0.5f), new Vector2(-0.5f, 0.5f) };
            for (int i = 0; i < quads; i++)
            {
                bool isCore = core && i == quads - 1;
                // x: 링마다 시작 시점을 고르게 어긋나게, y: 가운데 빛 표시
                var data = new Vector2(isCore ? 0f : (float)i / layers, isCore ? 1f : 0f);
                for (int k = 0; k < 4; k++)
                {
                    pos[i * 4 + k] = new Vector3(corners[k].x, 0f, corners[k].y);
                    corner[i * 4 + k] = corners[k];
                    layer[i * 4 + k] = data;
                }
                int v = i * 4, t = i * 6;
                tris[t] = v; tris[t + 1] = v + 2; tris[t + 2] = v + 1;
                tris[t + 3] = v; tris[t + 4] = v + 3; tris[t + 5] = v + 2;
            }
            mesh.vertices = pos;
            mesh.SetUVs(0, corner);
            mesh.SetUVs(1, layer);
            mesh.triangles = tris;
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        void Apply()
        {
            var r = GetComponent<MeshRenderer>();
            block ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor(ColorId, color);
            block.SetColor(CoreColorId, coreColor);
            block.SetFloat(IntensityId, intensity);
            block.SetFloat(SpeedId, speed);
            block.SetFloat(StartSizeId, startSize);
            block.SetFloat(EndSizeId, endSize);
            block.SetFloat(RiseId, rise);
            block.SetFloat(RiseCurveId, riseCurve);
            block.SetFloat(RingWidthId, ringWidth);
            block.SetFloat(RingSoftId, ringSoftness);
            block.SetFloat(InnerFillId, innerFill);
            block.SetFloat(FadeInId, fadeIn);
            block.SetFloat(FadeOutId, fadeOut);
            block.SetFloat(CoreStrengthId, coreStrength);
            block.SetFloat(CoreSizeId, coreSize);
            block.SetFloat(PulseId, pulse);
            block.SetFloat(DepthPullId, depthPull);
            r.SetPropertyBlock(block);
        }
    }
}

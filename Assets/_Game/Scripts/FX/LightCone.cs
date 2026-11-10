using UnityEngine;

namespace Archer
{
    /// <summary>
    /// 빛 콘(팡파레): 꼭짓점에서 원뿔 모양으로 빛이 은은하게 퍼진다. 오브젝트 +Y 로 넓어짐.
    /// 메시는 둘레(uv.x)와 밑동~끝(uv.y)만 담고, 실제 모양과 빛은 셰이더(Archer/LightCone)가 계산한다.
    /// 모든 값은 오브젝트마다 따로 (MaterialPropertyBlock).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class LightCone : MonoBehaviour
    {
        [Header("모양")]
        [Tooltip("높이 (오브젝트 단위)")]
        [Min(0.01f)] public float height = 1.6f;
        [Tooltip("밑동 반지름")]
        [Min(0f)] public float bottomRadius = 0.12f;
        [Tooltip("끝 반지름")]
        [Min(0.01f)] public float topRadius = 1.1f;
        [Tooltip("둘레 조각 수 (많을수록 둥글게)")]
        [Range(8, 64)] public int segments = 32;

        [Header("빛")]
        [ColorUsage(false, true)] public Color color = new Color(0.35f, 1f, 0.8f);
        [Min(0f)] public float intensity = 0.6f;
        [Tooltip("숨 쉬는 정도")]
        [Range(0f, 1f)] public float pulse = 0.25f;
        [Min(0f)] public float pulseSpeed = 1.2f;

        [Header("빛줄기")]
        [Range(1f, 40f)] public float rayCount = 14f;
        [Tooltip("클수록 가는 빛줄기")]
        [Range(1f, 32f)] public float raySharpness = 4f;
        [Tooltip("0 = 고르게, 1 = 빛줄기만")]
        [Range(0f, 1f)] public float rayContrast = 0.7f;
        [Tooltip("빛줄기 도는 속도")]
        [Range(-2f, 2f)] public float raySpeed = 0.1f;

        [Header("사라짐")]
        [Tooltip("밑동에서 서서히 나타나는 구간 (0~1)")]
        [Range(0.01f, 1f)] public float fadeInBottom = 0.15f;
        [Tooltip("끝으로 사라지는 곡선 (클수록 빨리 사라짐)")]
        [Range(0.2f, 6f)] public float fadeOutTop = 1.5f;
        [Tooltip("비스듬히 보이는 면을 흐리게 (판처럼 안 보이게)")]
        [Range(0f, 4f)] public float edgeSoftness = 1f;

        [Tooltip("벽에 묻히지 않게 화면 위치는 그대로 두고 카메라 쪽으로 당김 (m)")]
        [Range(0f, 3f)] public float depthPull = 0.3f;

        Mesh mesh;
        MaterialPropertyBlock block;
        int builtSegments = -1;
        bool dirty;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int HeightId = Shader.PropertyToID("_Height");
        static readonly int BottomRadiusId = Shader.PropertyToID("_BottomRadius");
        static readonly int TopRadiusId = Shader.PropertyToID("_TopRadius");
        static readonly int RayCountId = Shader.PropertyToID("_RayCount");
        static readonly int RaySharpId = Shader.PropertyToID("_RaySharp");
        static readonly int RayContrastId = Shader.PropertyToID("_RayContrast");
        static readonly int RaySpeedId = Shader.PropertyToID("_RaySpeed");
        static readonly int FadeBottomId = Shader.PropertyToID("_FadeBottom");
        static readonly int FadeTopId = Shader.PropertyToID("_FadeTop");
        static readonly int EdgeSoftId = Shader.PropertyToID("_EdgeSoft");
        static readonly int PulseId = Shader.PropertyToID("_Pulse");
        static readonly int PulseSpeedId = Shader.PropertyToID("_PulseSpeed");
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
            if (!mesh || builtSegments != segments) BuildMesh();
            // 셰이더가 모양을 정하니 화면 밖 판정 범위를 값에 맞춤
            float wide = Mathf.Max(topRadius, bottomRadius) * 2f;
            mesh.bounds = new Bounds(new Vector3(0f, height * 0.5f, 0f), new Vector3(wide, height, wide));
            Apply();
        }

        void BuildMesh()
        {
            if (!mesh) mesh = new Mesh { name = "LightCone", hideFlags = HideFlags.DontSave };
            mesh.Clear();
            builtSegments = segments;

            // 둘레 (segments + 1) x 위아래 2줄. 이음매는 uv.x 0 과 1 이 같은 자리
            int cols = segments + 1;
            var pos = new Vector3[cols * 2];
            var uv = new Vector2[cols * 2];
            for (int i = 0; i < cols; i++)
            {
                float u = (float)i / segments;
                for (int j = 0; j < 2; j++)
                {
                    pos[j * cols + i] = Vector3.zero; // 셰이더가 uv 로 위치를 정함
                    uv[j * cols + i] = new Vector2(u, j);
                }
            }
            var tris = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                int a = i, b = i + 1, c = cols + i, d = cols + i + 1, t = i * 6;
                tris[t] = a; tris[t + 1] = c; tris[t + 2] = b;
                tris[t + 3] = b; tris[t + 4] = c; tris[t + 5] = d;
            }
            mesh.vertices = pos;
            mesh.SetUVs(0, uv);
            mesh.triangles = tris;
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        void Apply()
        {
            var r = GetComponent<MeshRenderer>();
            block ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor(ColorId, color);
            block.SetFloat(IntensityId, intensity);
            block.SetFloat(HeightId, height);
            block.SetFloat(BottomRadiusId, bottomRadius);
            block.SetFloat(TopRadiusId, topRadius);
            block.SetFloat(RayCountId, rayCount);
            block.SetFloat(RaySharpId, raySharpness);
            block.SetFloat(RayContrastId, rayContrast);
            block.SetFloat(RaySpeedId, raySpeed);
            block.SetFloat(FadeBottomId, fadeInBottom);
            block.SetFloat(FadeTopId, fadeOutTop);
            block.SetFloat(EdgeSoftId, edgeSoftness);
            block.SetFloat(PulseId, pulse);
            block.SetFloat(PulseSpeedId, pulseSpeed);
            block.SetFloat(DepthPullId, depthPull);
            r.SetPropertyBlock(block);
        }
    }
}

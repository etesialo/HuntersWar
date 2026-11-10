using UnityEngine;

namespace Archer
{
    /// <summary>
    /// 반딧불: 동그랗게 빛나는 글로우들이 아래에서 위로 천천히 올라가며 사라진다.
    /// 오브젝트 위치 = 바닥 가운데. 가로(area.x) x 세로(area.y) 바닥에서 height 만큼 올라감.
    /// 움직임은 셰이더(Archer/Firefly)가 하고, 여기서는 글로우 개수만큼 쿼드를 만들고 값을 넘긴다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class FireflyField : MonoBehaviour
    {
        [Header("영역")]
        [Min(1)] public int count = 20;
        [Tooltip("바닥 영역 가로(x) x 세로(z), m")]
        public Vector2 area = new Vector2(4f, 4f);
        [Tooltip("올라가는 높이 (m)")]
        [Min(0.1f)] public float height = 3f;

        [Header("모양")]
        [ColorUsage(false, true)] public Color color = new Color(1f, 0.85f, 0.4f);
        [Min(0f)] public float intensity = 1.5f;
        [Tooltip("글로우 크기 (m)")]
        [Min(0.01f)] public float size = 0.15f;
        [Tooltip("글로우마다 크기 차이 (0 = 모두 같음)")]
        [Range(0f, 1f)] public float sizeVariation = 0.3f;
        [Tooltip("원 가장자리 부드러움 (0 = 또렷한 원, 1 = 가운데부터 흐려짐)")]
        [Range(0f, 1f)] public float feather = 0.6f;
        [Tooltip("가운데를 더 밝게 (0 = 고르게)")]
        [Range(0f, 2f)] public float centerBrightness = 0.3f;
        [Tooltip("바깥으로 번지는 빛무리 크기 (원 크기의 몇 배, 1 = 빛무리 없음)")]
        [Range(1f, 6f)] public float haloSize = 2.5f;
        [Tooltip("빛무리 세기")]
        [Range(0f, 3f)] public float haloStrength = 0.8f;
        [Tooltip("깜빡임 (0 = 안 깜빡임)")]
        [Range(0f, 1f)] public float twinkle = 0.3f;

        [Header("움직임")]
        [Tooltip("올라가는 속도 (m/s)")]
        [Min(0f)] public float speed = 0.4f;
        [Tooltip("글로우마다 속도 차이 (0 = 모두 같음, 0.4 = ±40%)")]
        [Range(0f, 0.9f)] public float speedVariation = 0.4f;
        [Tooltip("좌우로 살랑이는 폭 (m)")]
        [Min(0f)] public float sway = 0.1f;

        [Header("사라지는 영역 (높이 비율)")]
        [Tooltip("아래에서 이만큼 올라가는 동안 서서히 나타남")]
        [Range(0f, 1f)] public float fadeInBottom = 0.2f;
        [Tooltip("위에서 이만큼 남았을 때부터 서서히 사라짐")]
        [Range(0f, 1f)] public float fadeOutTop = 0.35f;

        [Tooltip("배치 무늬 (바꾸면 글로우 위치가 새로 섞임)")]
        public int seed = 1;

        Mesh mesh;
        MaterialPropertyBlock block;
        int builtHash;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int SpeedId = Shader.PropertyToID("_Speed");
        static readonly int SpeedVarId = Shader.PropertyToID("_SpeedVar");
        static readonly int SizeId = Shader.PropertyToID("_Size");
        static readonly int SizeVarId = Shader.PropertyToID("_SizeVar");
        static readonly int HeightId = Shader.PropertyToID("_Height");
        static readonly int FadeBottomId = Shader.PropertyToID("_FadeBottom");
        static readonly int FadeTopId = Shader.PropertyToID("_FadeTop");
        static readonly int SwayId = Shader.PropertyToID("_Sway");
        static readonly int TwinkleId = Shader.PropertyToID("_Twinkle");
        static readonly int FeatherId = Shader.PropertyToID("_Feather");
        static readonly int CenterBrightId = Shader.PropertyToID("_CenterBright");
        static readonly int HaloSizeId = Shader.PropertyToID("_HaloSize");
        static readonly int HaloStrengthId = Shader.PropertyToID("_HaloStrength");

        bool dirty;

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
            // 개수·영역·무늬가 바뀔 때만 메시를 다시 만든다
            int hash = count ^ (area.GetHashCode() * 31) ^ (height.GetHashCode() * 17) ^ (seed * 7919) ^ (size.GetHashCode() * 13) ^ (sway.GetHashCode() * 3) ^ (haloSize.GetHashCode() * 5);
            if (!mesh || hash != builtHash)
            {
                BuildMesh();
                builtHash = hash;
            }
            Apply();
        }

        void BuildMesh()
        {
            if (!mesh)
            {
                mesh = new Mesh { name = "Fireflies", hideFlags = HideFlags.DontSave };
                mesh.MarkDynamic();
            }
            mesh.Clear();
            var rnd = new System.Random(seed);
            var pos = new Vector3[count * 4];
            var corner = new Vector2[count * 4];
            var data = new Vector4[count * 4];
            var tris = new int[count * 6];
            var corners = new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(0.5f, 0.5f), new Vector2(-0.5f, 0.5f) };
            for (int i = 0; i < count; i++)
            {
                var p = new Vector3(((float)rnd.NextDouble() - 0.5f) * area.x, 0f, ((float)rnd.NextDouble() - 0.5f) * area.y);
                var d = new Vector4((float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(), 0f);
                for (int k = 0; k < 4; k++)
                {
                    pos[i * 4 + k] = p;
                    corner[i * 4 + k] = corners[k];
                    data[i * 4 + k] = d;
                }
                int v = i * 4, t = i * 6;
                tris[t] = v; tris[t + 1] = v + 2; tris[t + 2] = v + 1;
                tris[t + 3] = v; tris[t + 4] = v + 3; tris[t + 5] = v + 2;
            }
            mesh.vertices = pos;
            mesh.SetUVs(0, corner);
            mesh.SetUVs(1, data);
            mesh.triangles = tris;
            // 셰이더가 위로 올리고 흔드니 화면 밖으로 잘리지 않게 넉넉히
            float pad = size * haloSize * 2f + sway;
            mesh.bounds = new Bounds(new Vector3(0f, height * 0.5f, 0f), new Vector3(area.x + pad * 2f, height + pad * 2f, area.y + pad * 2f));
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        void Apply()
        {
            var r = GetComponent<MeshRenderer>();
            block ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor(ColorId, color);
            block.SetFloat(IntensityId, intensity);
            block.SetFloat(SpeedId, speed);
            block.SetFloat(SpeedVarId, speedVariation);
            block.SetFloat(SizeId, size);
            block.SetFloat(SizeVarId, sizeVariation);
            block.SetFloat(HeightId, height);
            block.SetFloat(FadeBottomId, fadeInBottom);
            block.SetFloat(FadeTopId, fadeOutTop);
            block.SetFloat(SwayId, sway);
            block.SetFloat(TwinkleId, twinkle);
            block.SetFloat(FeatherId, feather);
            block.SetFloat(CenterBrightId, centerBrightness);
            block.SetFloat(HaloSizeId, haloSize);
            block.SetFloat(HaloStrengthId, haloStrength);
            r.SetPropertyBlock(block);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(color.r, color.g, color.b, 0.5f);
            Gizmos.DrawWireCube(new Vector3(0f, height * 0.5f, 0f), new Vector3(area.x, height, area.y));
        }
#endif
    }
}

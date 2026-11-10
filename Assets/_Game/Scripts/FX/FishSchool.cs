using UnityEngine;

namespace Archer
{
    /// <summary>
    /// 물속 작은 물고기: 위에서 본 물고기 실루엣들이 영역 안을 이리저리 유영한다.
    /// 오브젝트 위치 = 물 표면 높이의 영역 가운데. 물고기는 depth 만큼 아래에서 헤엄친다.
    /// 움직임은 셰이더(Archer/FishSchool)가 하고, 여기서는 마리 수만큼 쿼드를 만들고 값을 넘긴다.
    /// 물 다음에 옅은 물속 그림자처럼 그린다 (물이 깊은 곳을 불투명하게 칠해서 그 아래는 안 보이므로).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class FishSchool : MonoBehaviour
    {
        [Header("영역")]
        [Range(1, 30)] public int count = 4;
        [Tooltip("헤엄치는 영역 가로(x) x 세로(z), m")]
        public Vector2 area = new Vector2(2f, 2f);
        [Tooltip("물 표면(오브젝트 위치)에서 얼마나 아래 (m)")]
        [Min(0f)] public float depth = 0.15f;

        [Header("모양")]
        [Tooltip("물빛보다 어두운 색이 물속에 잠긴 느낌")]
        public Color color = new Color(0.08f, 0.3f, 0.32f);
        [Range(0f, 1f)] public float opacity = 0.45f;
        [Tooltip("몸 길이 (m)")]
        [Min(0.01f)] public float size = 0.5f;
        [Tooltip("마리마다 크기 차이")]
        [Range(0f, 0.8f)] public float sizeVariation = 0.3f;
        [Tooltip("가장자리 흐림")]
        [Range(0.02f, 1f)] public float feather = 0.3f;

        [Header("움직임")]
        [Min(0f)] public float speed = 0.4f;
        [Tooltip("꼬리·몸 흔들림")]
        [Range(0f, 1f)] public float wiggle = 0.25f;
        [Min(0f)] public float wiggleSpeed = 8f;

        [Tooltip("마리마다 경로 무늬 (바꾸면 새로 섞임)")]
        public int seed = 1;

        Mesh mesh;
        MaterialPropertyBlock block;
        int builtHash;
        bool dirty;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        static readonly int SizeId = Shader.PropertyToID("_Size");
        static readonly int SizeVarId = Shader.PropertyToID("_SizeVar");
        static readonly int AreaXId = Shader.PropertyToID("_AreaX");
        static readonly int AreaZId = Shader.PropertyToID("_AreaZ");
        static readonly int SpeedId = Shader.PropertyToID("_Speed");
        static readonly int WiggleId = Shader.PropertyToID("_Wiggle");
        static readonly int WiggleSpeedId = Shader.PropertyToID("_WiggleSpeed");
        static readonly int FeatherId = Shader.PropertyToID("_Feather");
        static readonly int DepthId = Shader.PropertyToID("_Depth");

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
            int hash = count * 31 + seed * 7919;
            if (!mesh || hash != builtHash)
            {
                BuildMesh();
                builtHash = hash;
            }
            // 셰이더가 영역 안에서 움직이니 화면 밖 판정 범위를 값에 맞춤
            float pad = size * 2f;
            mesh.bounds = new Bounds(new Vector3(0f, -depth, 0f), new Vector3(area.x + pad, 0.2f, area.y + pad));
            Apply();
        }

        void BuildMesh()
        {
            if (!mesh) mesh = new Mesh { name = "Fish", hideFlags = HideFlags.DontSave };
            mesh.Clear();
            var rnd = new System.Random(seed);
            var pos = new Vector3[count * 4];
            var corner = new Vector2[count * 4];
            var data = new Vector4[count * 4];
            var tris = new int[count * 6];
            var corners = new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(0.5f, 0.5f), new Vector2(-0.5f, 0.5f) };
            for (int i = 0; i < count; i++)
            {
                var d = new Vector4((float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble());
                for (int k = 0; k < 4; k++)
                {
                    pos[i * 4 + k] = Vector3.zero; // 셰이더가 위치를 정함
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
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        void Apply()
        {
            var r = GetComponent<MeshRenderer>();
            block ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor(ColorId, color);
            block.SetFloat(OpacityId, opacity);
            block.SetFloat(SizeId, size);
            block.SetFloat(SizeVarId, sizeVariation);
            block.SetFloat(AreaXId, area.x);
            block.SetFloat(AreaZId, area.y);
            block.SetFloat(SpeedId, speed);
            block.SetFloat(WiggleId, wiggle);
            block.SetFloat(WiggleSpeedId, wiggleSpeed);
            block.SetFloat(FeatherId, feather);
            block.SetFloat(DepthId, depth);
            r.SetPropertyBlock(block);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireCube(new Vector3(0f, -depth, 0f), new Vector3(area.x, 0f, area.y));
        }
#endif
    }
}

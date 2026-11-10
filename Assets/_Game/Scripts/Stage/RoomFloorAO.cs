using UnityEngine;

namespace Archer
{
    /// <summary>
    /// 바닥 접지 AO. 벽(그리드 바깥 한 줄)과 장애물(Obstacles 의 BoxCollider 발자국) 둘레의 바닥을
    /// 어둡게 하는 마스크 텍스처를 만들어 전역(_RoomFloorAO)으로 넘긴다.
    /// 바닥 셰이더(Archer/StylizedEnv, Floor Contact AO 켬)가 이걸 읽는다.
    /// 텍스처는 저장하지 않고, 배치가 바뀌었을 때만 다시 계산·업로드한다.
    /// </summary>
    [ExecuteAlways]
    public class RoomFloorAO : MonoBehaviour
    {
        [Tooltip("어두워지는 폭 (m)")] [Range(0.1f, 1.5f)] public float radius = 0.55f;
        [Tooltip("벽 쪽 세기")] [Range(0f, 1f)] public float wallStrength = 0.9f;
        [Tooltip("장애물 쪽 세기")] [Range(0f, 1f)] public float obstacleStrength = 1f;
        [Range(4, 16)] public int pixelsPerCell = 8;

        static readonly int TexId = Shader.PropertyToID("_RoomFloorAO");
        static readonly int RectId = Shader.PropertyToID("_RoomFloorAORect");
        Texture2D tex;
        int lastHash;
        bool uploaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        static void ResetGlobal()
        {
            Shader.SetGlobalTexture(TexId, Texture2D.blackTexture);
            Shader.SetGlobalVector(RectId, new Vector4(-RoomGrid.Width * 0.5f, -RoomGrid.Length * 0.5f, 1f / RoomGrid.Width, 1f / RoomGrid.Length));
        }

        void OnEnable() => Rebuild();

        void OnDisable()
        {
            if (tex)
            {
                if (Shader.GetGlobalTexture(TexId) == tex) Shader.SetGlobalTexture(TexId, Texture2D.blackTexture);
                if (Application.isPlaying) Destroy(tex); else DestroyImmediate(tex);
                tex = null;
            }
            uploaded = false;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () => { if (this && isActiveAndEnabled) Rebuild(); };
        }
#endif

        public void Rebuild()
        {
            if (!gameObject.scene.IsValid()) return; // 프리팹 원본 에셋에서는 안 함

            // 장애물 발자국 (방 기준 xz 사각형)
            var rects = new System.Collections.Generic.List<Vector4>(); // (minX, minZ, maxX, maxZ)
            var ob = transform.Find("Obstacles");
            if (ob)
                foreach (Transform o in ob)
                {
                    if (!o.TryGetComponent<BoxCollider>(out var box)) continue;
                    var c = transform.InverseTransformPoint(o.TransformPoint(box.center));
                    var size = Vector3.Scale(box.size, o.lossyScale);
                    rects.Add(new Vector4(c.x - size.x * 0.5f, c.z - size.z * 0.5f, c.x + size.x * 0.5f, c.z + size.z * 0.5f));
                }

            // 바뀐 게 없으면 다시 안 만든다
            int hash = 17;
            unchecked
            {
                foreach (var r in rects) hash = hash * 31 + r.GetHashCode();
                hash = hash * 31 + radius.GetHashCode() + wallStrength.GetHashCode() * 7 + obstacleStrength.GetHashCode() * 13 + pixelsPerCell;
            }
            if (uploaded && hash == lastHash && tex) { Publish(); return; }

            int w = RoomGrid.Cols * pixelsPerCell, h = RoomGrid.Rows * pixelsPerCell;
            if (tex == null || tex.width != w || tex.height != h)
            {
                if (tex) { if (Application.isPlaying) Destroy(tex); else DestroyImmediate(tex); }
                tex = new Texture2D(w, h, TextureFormat.R8, false, true)
                {
                    name = "RoomFloorAO", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave
                };
            }

            float minX = -RoomGrid.Width * 0.5f, minZ = -RoomGrid.Length * 0.5f;
            float step = RoomGrid.Cell / pixelsPerCell;
            float hw = RoomGrid.InnerHalfW, hl = RoomGrid.InnerHalfL;
            var px = new byte[w * h];
            for (int y = 0; y < h; y++)
            {
                float z = minZ + (y + 0.5f) * step;
                for (int x = 0; x < w; x++)
                {
                    float wx = minX + (x + 0.5f) * step;
                    // 벽: 안쪽 영역 경계까지 거리
                    float dWall = Mathf.Min(hw - Mathf.Abs(wx), hl - Mathf.Abs(z));
                    float a = dWall <= 0f ? 1f : Fall(dWall) * wallStrength;
                    // 장애물: 사각형까지 거리
                    foreach (var r in rects)
                    {
                        float dx = Mathf.Max(r.x - wx, 0f, wx - r.z);
                        float dz = Mathf.Max(r.y - z, 0f, z - r.w);
                        float d = Mathf.Sqrt(dx * dx + dz * dz);
                        a = Mathf.Max(a, (d <= 0f ? 1f : Fall(d)) * obstacleStrength);
                    }
                    px[y * w + x] = (byte)(Mathf.Clamp01(a) * 255f);
                }
            }
            tex.SetPixelData(px, 0);
            tex.Apply(false);
            lastHash = hash;
            uploaded = true;
            Publish();
        }

        /// <summary>경계에서 멀어질수록 부드럽게 0으로</summary>
        float Fall(float d)
        {
            float t = Mathf.Clamp01(1f - d / radius);
            return t * t * (3f - 2f * t);
        }

        void Publish()
        {
            var o = transform.position;
            Shader.SetGlobalTexture(TexId, tex);
            Shader.SetGlobalVector(RectId, new Vector4(o.x - RoomGrid.Width * 0.5f, o.z - RoomGrid.Length * 0.5f,
                1f / RoomGrid.Width, 1f / RoomGrid.Length));
        }
    }
}

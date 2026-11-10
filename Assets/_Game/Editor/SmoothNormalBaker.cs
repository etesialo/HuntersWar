using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Archer.EditorTools
{
    /// <summary>
    /// Art/Mesh 의 모델을 가져올 때 '부드러운 노멀'을 UV3(TEXCOORD3)에 저장한다.
    /// 각 꼭짓점 주변 Radius 안에 있는 꼭짓점들의 노멀을 거리 가중 평균한다.
    /// (같은 자리만 평균하면 챔퍼가 있는 모델은 거의 안 부드러워져서 반경으로 넓힘)
    /// StylizedEnv 의 Soft Normals(Smoothed 모드)가 이걸 섞어 쓴다. 모양에 상관없이(벽·고리·계단) 동작.
    /// </summary>
    public class SmoothNormalBaker : AssetPostprocessor
    {
        const string Folder = "Assets/_Game/Art/Mesh/";
        /// <summary>부드럽게 이어지는 범위 (m). 바꾸면 GetVersion 도 올려서 다시 가져오게</summary>
        public const float Radius = 0.25f;
        /// <summary>정점 AO 를 찾는 범위 (m). 이 안의 오목한 구석이 어두워진다</summary>
        public const float AORadius = 0.45f;
        public override uint GetVersion() => 3;

        void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.Replace('\\', '/').StartsWith(Folder)) return;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh) Bake(mf.sharedMesh, mf.transform.lossyScale);
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.sharedMesh) Bake(smr.sharedMesh, smr.transform.lossyScale);
        }

        public static void Bake(Mesh mesh, Vector3 scale)
        {
            var v = mesh.vertices;
            var n = mesh.normals;
            if (v.Length == 0 || n.Length != v.Length) return;

            // 반경은 월드(m) 기준 → 메시 좌표로 환산
            float s = Mathf.Max(1e-4f, (Mathf.Abs(scale.x) + Mathf.Abs(scale.y) + Mathf.Abs(scale.z)) / 3f);
            float r = Radius / s, r2 = r * r;

            // 격자 해시로 이웃 찾기
            var grid = new Dictionary<Vector3Int, List<int>>();
            Vector3Int Cell(Vector3 p) => new(Mathf.FloorToInt(p.x / r), Mathf.FloorToInt(p.y / r), Mathf.FloorToInt(p.z / r));
            for (int i = 0; i < v.Length; i++)
            {
                var c = Cell(v[i]);
                if (!grid.TryGetValue(c, out var l)) grid[c] = l = new List<int>();
                l.Add(i);
            }

            // xyz = 부드러운 노멀, w = 정점 AO (0 = 안 가려짐, 1 = 완전히 가려짐)
            var smooth = new List<Vector4>(v.Length);
            float aoR = AORadius / s, aoR2 = aoR * aoR;
            for (int i = 0; i < v.Length; i++)
            {
                var p = v[i];
                var c = Cell(p);
                var sum = Vector3.zero;
                float occl = 0f, occW = 0f;
                int span = Mathf.CeilToInt(Mathf.Max(r, aoR) / r);
                for (int dx = -span; dx <= span; dx++)
                for (int dy = -span; dy <= span; dy++)
                for (int dz = -span; dz <= span; dz++)
                {
                    if (!grid.TryGetValue(new Vector3Int(c.x + dx, c.y + dy, c.z + dz), out var l)) continue;
                    foreach (int j in l)
                    {
                        var to = v[j] - p;
                        float d2 = to.sqrMagnitude;
                        // 부드러운 노멀: 가까울수록 크게, 반대쪽을 보는 면(얇은 벽 뒷면)은 섞지 않음
                        if (d2 <= r2 && Vector3.Dot(n[j], n[i]) >= -0.2f) sum += n[j] * (1f - d2 / r2);
                        // AO: 내 노멀 앞쪽에 있고 나를 마주 보는 면이 가까울수록 가려짐 (오목한 구석)
                        if (d2 > 1e-8f && d2 <= aoR2)
                        {
                            float d = Mathf.Sqrt(d2);
                            var dir = to / d;
                            float front = Vector3.Dot(n[i], dir);          // 내 앞에 있는가
                            float facing = -Vector3.Dot(n[j], dir);        // 나를 보고 있는가
                            float w = 1f - d / aoR;
                            occW += w;
                            if (front > 0.15f && facing > 0.05f) occl += front * w;
                        }
                    }
                }
                float ao = occW > 0f ? Mathf.Clamp01(occl / occW * 2.5f) : 0f;
                var sn = sum.sqrMagnitude > 1e-8f ? sum.normalized : n[i];
                smooth.Add(new Vector4(sn.x, sn.y, sn.z, ao));
            }
            mesh.SetUVs(3, smooth);
        }
    }
}

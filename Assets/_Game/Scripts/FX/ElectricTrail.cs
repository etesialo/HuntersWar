using System.Collections.Generic;
using UnityEngine;

namespace Archer
{
    /// <summary>
    /// 번개 화살 궤적: 지나온 길을 따라 지지직거리는 직선 전기 줄기.
    /// 방울 입자 대신, 지나간 경로에 LineRenderer 두 줄(밝은 심 + 푸른 번짐)을 긋고
    /// 아주 짧은 간격으로 옆으로 들쭉날쭉 흔들어 노이즈처럼 보이게 한다. 오래된 구간부터 사라짐.
    /// </summary>
    public class ElectricTrail : MonoBehaviour
    {
        [Tooltip("궤적이 남아 있는 시간 (초)")] public float life = 0.22f;
        [Tooltip("궤적 최대 길이 (m)")] public float maxLength = 3.2f;
        [Tooltip("옆으로 튀는 폭 (m)")] public float jitter = 0.09f;
        [Tooltip("다시 흔드는 간격 (초)")] public float interval = 0.03f;
        [Tooltip("궤적을 몇 마디로 나눌지 (많을수록 잘게 지지직)")] public int segments = 14;
        public Color core = new(0.92f, 0.97f, 1f, 1f);
        public Color glow = new(0.35f, 0.65f, 1f, 0.7f);
        public float coreWidth = 0.05f;
        public float glowWidth = 0.16f;
        public Material material;

        struct P { public Vector3 pos; public float t; }
        readonly List<P> path = new();
        LineRenderer coreLine, glowLine;
        float timer;
        static Material fallback;
        Vector3[] buf;

        void Awake() => Build();

        void Build()
        {
            if (coreLine) return;
            var mat = material;
            if (!mat)
            {
                if (!fallback) fallback = new Material(Shader.Find("Sprites/Default")) { name = "ElectricTrail (auto)" };
                mat = fallback;
            }
            glowLine = MakeLine("TrailGlow", mat, 0);
            coreLine = MakeLine("TrailCore", mat, 1);
        }

        LineRenderer MakeLine(string n, Material mat, int order)
        {
            var go = new GameObject(n);
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.numCapVertices = 0;
            lr.numCornerVertices = 0;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sharedMaterial = mat;
            lr.sortingOrder = order;
            lr.positionCount = 0;
            return lr;
        }

        /// <summary>풀에서 다시 쏠 때: 예전 궤적 지우기</summary>
        public void ResetTrail()
        {
            Build();
            path.Clear();
            coreLine.positionCount = 0;
            glowLine.positionCount = 0;
            timer = 0f;
        }

        void OnEnable() => ResetTrail();

        void LateUpdate()
        {
            float now = Time.time;
            var head = transform.position;
            if (path.Count == 0 || (head - path[path.Count - 1].pos).sqrMagnitude > 0.0004f)
                path.Add(new P { pos = head, t = now });

            // 오래된 점 / 너무 긴 꼬리 정리 (화살이 벽에 멈추면 새 점이 안 생겨서 자연히 줄어듦)
            while (path.Count > 0 && now - path[0].t > life) path.RemoveAt(0);
            float len = 0f;
            for (int i = path.Count - 1; i > 0; i--)
            {
                len += (path[i].pos - path[i - 1].pos).magnitude;
                if (len > maxLength) { path.RemoveRange(0, i - 1); break; }
            }

            timer -= Time.deltaTime;
            if (timer > 0f && Time.deltaTime > 0f) return;
            timer = interval;
            Draw(now);
        }

        void Draw(float now)
        {
            if (path.Count < 2)
            {
                coreLine.positionCount = 0;
                glowLine.positionCount = 0;
                return;
            }
            int n = segments + 1;
            if (buf == null || buf.Length != n) buf = new Vector3[n];

            // 경로를 고르게 n 점으로 나눈 뒤, 양 끝(머리/꼬리)만 빼고 옆으로 들쭉날쭉
            float total = 0f;
            for (int i = 1; i < path.Count; i++) total += (path[i].pos - path[i - 1].pos).magnitude;
            var dir = (path[path.Count - 1].pos - path[0].pos);
            dir.y = 0f;
            var side = Vector3.Cross(Vector3.up, dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward);
            for (int k = 0; k < n; k++)
            {
                float target = total * k / (n - 1);
                var p = Sample(target);
                if (k > 0 && k < n - 1)
                {
                    float amp = jitter * (0.4f + 0.6f * (float)k / (n - 1)); // 머리 쪽이 더 크게 튐
                    p += side * Random.Range(-amp, amp) + Vector3.up * Random.Range(-amp, amp) * 0.4f;
                }
                buf[k] = p;
            }

            // 꼬리(오래된 쪽)는 투명, 머리 쪽은 진하게
            float age = Mathf.Clamp01((now - path[0].t) / life);
            Apply(coreLine, core, coreWidth, age);
            Apply(glowLine, glow, glowWidth, age);
        }

        void Apply(LineRenderer lr, Color c, float width, float age)
        {
            lr.positionCount = buf.Length;
            lr.SetPositions(buf);
            lr.startColor = new Color(c.r, c.g, c.b, 0f);
            lr.endColor = c;
            lr.widthCurve = AnimationCurve.Linear(0f, width * 0.3f, 1f, width);
            lr.widthMultiplier = Random.Range(0.75f, 1.25f);
        }

        Vector3 Sample(float dist)
        {
            for (int i = 1; i < path.Count; i++)
            {
                float d = (path[i].pos - path[i - 1].pos).magnitude;
                if (dist <= d || i == path.Count - 1)
                    return Vector3.Lerp(path[i - 1].pos, path[i].pos, d > 1e-5f ? Mathf.Clamp01(dist / d) : 1f);
                dist -= d;
            }
            return path[path.Count - 1].pos;
        }
    }
}

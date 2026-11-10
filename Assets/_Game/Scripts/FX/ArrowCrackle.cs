using UnityEngine;

namespace Archer
{
    /// <summary>
    /// 번개 화살: 화살 몸통 주변에 짧은 전기 줄기가 지글지글 튄다.
    /// 자식 LineRenderer 몇 개를 짧은 간격으로 다시 그리고, 가끔 꺼서 깜빡이게 한다.
    /// </summary>
    public class ArrowCrackle : MonoBehaviour
    {
        [Tooltip("전기 줄기 개수")] [Range(1, 4)] public int count = 2;
        [Tooltip("줄기 길이 (화살 뒤쪽으로, m)")] public float length = 0.9f;
        [Tooltip("옆으로 튀는 폭 (m)")] public float spread = 0.16f;
        [Tooltip("다시 그리는 간격 (초)")] public float interval = 0.045f;
        [Range(0f, 1f)] public float flickerOff = 0.25f;
        public float width = 0.05f;
        public Color color = new(0.85f, 0.93f, 1f, 1f);
        public Material material;

        const int Points = 6;
        LineRenderer[] lines;
        float timer;
        static Material fallback;

        void Awake() => Build();

        void Build()
        {
            if (lines != null) return;
            var mat = material;
            if (!mat)
            {
                if (!fallback) fallback = new Material(Shader.Find("Sprites/Default")) { name = "ArrowCrackle (auto)" };
                mat = fallback;
            }
            lines = new LineRenderer[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Crackle" + i);
                go.hideFlags = HideFlags.DontSave;
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.positionCount = Points;
                lr.numCapVertices = 1;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.sharedMaterial = mat;
                lr.startColor = color;
                lr.endColor = new Color(color.r, color.g, color.b, 0f);
                lr.widthMultiplier = width;
                lines[i] = lr;
            }
        }

        void OnEnable()
        {
            Build();
            timer = 0f;
        }

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = interval;
            foreach (var lr in lines)
            {
                bool on = Random.value > flickerOff;
                lr.enabled = on;
                if (!on) continue;
                // 화살 머리 근처에서 시작해 뒤쪽으로 지그재그
                float start = Random.Range(-0.1f, 0.15f);
                float side = Random.Range(-1f, 1f);
                for (int p = 0; p < Points; p++)
                {
                    float t = (float)p / (Points - 1);
                    float z = start - t * length * Random.Range(0.6f, 1f);
                    float x = (side * 0.3f + Random.Range(-1f, 1f)) * spread * t;
                    float y = Random.Range(-1f, 1f) * spread * 0.6f * t;
                    lr.SetPosition(p, new Vector3(x, y, z));
                }
                lr.widthMultiplier = width * Random.Range(0.7f, 1.3f);
            }
        }
    }
}

using UnityEngine;

namespace Archer
{
    /// <summary>어떤 화면비에서도 9:16 영역만 쓰고 나머지는 레터박스 처리.</summary>
    [RequireComponent(typeof(Camera))]
    public class AspectLock : MonoBehaviour
    {
        public float width = 9f;
        public float height = 16f;

        Camera cam;
        int lastW, lastH;

        void Awake()
        {
            cam = GetComponent<Camera>();
            Apply();
        }

        void Update()
        {
            if (Screen.width != lastW || Screen.height != lastH) Apply();
        }

        void Apply()
        {
            lastW = Screen.width;
            lastH = Screen.height;
            if (lastW <= 0 || lastH <= 0) return;

            float target = width / height;
            float screen = (float)lastW / lastH;
            if (screen > target)
            {
                float w = target / screen;
                cam.rect = new Rect((1f - w) * 0.5f, 0f, w, 1f);
            }
            else
            {
                float h = screen / target;
                cam.rect = new Rect(0f, (1f - h) * 0.5f, 1f, h);
            }
        }
    }
}

using System.Collections;
using UnityEngine;

namespace Archer
{
    /// <summary>
    /// 히트스톱: 강한 타격 순간 게임을 아주 잠깐 거의 멈춰 "묵직하게" 느끼게 한다.
    /// 일시정지/선택 창(timeScale 0) 중에는 건드리지 않고, 끝나면 진행 중일 때만 1로 돌린다.
    /// </summary>
    public static class HitStop
    {
        static float until;
        static bool running;

        public static void Do(float seconds, float scale = 0.05f)
        {
            var gm = GameManager.I;
            if (!gm || gm.State != GameState.Playing || seconds <= 0f) return;
            until = Mathf.Max(until, Time.unscaledTime + seconds);
            Time.timeScale = scale;
            if (!running) gm.StartCoroutine(Run());
        }

        static IEnumerator Run()
        {
            running = true;
            while (Time.unscaledTime < until) yield return null;
            running = false;
            var gm = GameManager.I;
            if (gm && gm.State == GameState.Playing) Time.timeScale = 1f;
        }
    }

    /// <summary>모바일 진동 (에디터/PC 에선 아무것도 안 함)</summary>
    public static class Haptics
    {
        public static void Light()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }
    }
}

using System.Collections;
using UnityEngine;

namespace Archer
{
    /// <summary>
    /// 깨지 않은 방의 출구를 막는 보라색 연기 장막. StageManager 가 출구에 세우고, 방을 깨면 Open() 으로 걷는다.
    /// (판정은 원래 문(door)이 하고, 이건 보이는 것만)
    /// </summary>
    public class ExitBarrier : MonoBehaviour
    {
        public float openTime = 0.6f;

        static readonly int FadeId = Shader.PropertyToID("_Fade");
        Renderer[] renderers;
        MaterialPropertyBlock block;

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            block = new MaterialPropertyBlock();
            SetFade(1f);
        }

        /// <summary>출구(벽 줄 위의 2칸 표식)에 맞춰 세운다. side: RoomBuilder.ExitSide</summary>
        public void Place(Transform room, Vector3 exitLocal)
        {
            // 장막 앞면(+z)이 방 안쪽을 보게
            float yaw = RoomBuilder.ExitSide(exitLocal) switch { 0 => 180f, 1 => 0f, 2 => 90f, _ => -90f };
            transform.SetPositionAndRotation(room.TransformPoint(exitLocal), room.rotation * Quaternion.Euler(0f, yaw, 0f));
        }

        public void Open()
        {
            if (!isActiveAndEnabled) return;
            StartCoroutine(FadeOut());
        }

        IEnumerator FadeOut()
        {
            for (float t = 0f; t < openTime; t += Time.deltaTime)
            {
                SetFade(1f - t / openTime);
                yield return null;
            }
            gameObject.SetActive(false);
        }

        void SetFade(float f)
        {
            foreach (var r in renderers)
            {
                if (!r) continue;
                r.GetPropertyBlock(block);
                block.SetFloat(FadeId, f);
                r.SetPropertyBlock(block);
            }
        }
    }
}

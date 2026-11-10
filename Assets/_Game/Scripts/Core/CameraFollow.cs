using UnityEngine;

namespace Archer
{
    /// <summary>쿼터뷰 카메라. 방 가로폭은 고정으로 보여주고 세로(z)만 플레이어를 따라간다.</summary>
    [DefaultExecutionOrder(-50)]
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public float pitch = 60f;
        public float distance = 30f;
        [Tooltip("플레이어보다 얼마나 앞쪽을 비출지")]
        public float lookAhead = 3f;
        public float minZ = -1f;
        public float maxZ = 1.5f;
        public float smooth = 6f;

        [Tooltip("켜면 씬에 놓아둔 카메라의 각도·거리를 그대로 쓴다 (pitch/distance 값 무시)")]
        public bool useScenePose = true;

        Vector3 focus;
        float shakeTime, shakeAmp;

        void Awake()
        {
            if (!useScenePose) return;
            // 씬에서 놓아둔 위치에서 바닥(y=0)까지 시선 거리 = distance
            var fwd = transform.forward;
            if (fwd.y < -0.01f)
            {
                pitch = transform.eulerAngles.x;
                distance = -transform.position.y / fwd.y;
            }
        }

        Vector3 Desired()
        {
            float z = target ? Mathf.Clamp(target.position.z + lookAhead, minZ, maxZ) : 0f;
            return new Vector3(0f, 0f, z);
        }

        void LateUpdate()
        {
            if (!target) return;
            focus = Vector3.Lerp(focus, Desired(), 1f - Mathf.Exp(-smooth * Time.deltaTime));
            Apply();
        }

        void Apply()
        {
            var rot = Quaternion.Euler(pitch, 0f, 0f);
            var pos = focus - rot * Vector3.forward * distance;
            if (shakeTime > 0f && Time.deltaTime > 0f)
            {
                shakeTime -= Time.deltaTime;
                pos += Random.insideUnitSphere * shakeAmp;
            }
            transform.SetPositionAndRotation(pos, rot);
        }

        public void Snap()
        {
            focus = Desired();
            Apply();
        }

        public void Shake(float amp, float time)
        {
            shakeAmp = shakeTime > 0f ? Mathf.Max(amp, shakeAmp) : amp;
            shakeTime = Mathf.Max(shakeTime, time);
        }
    }
}

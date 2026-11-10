using System.Collections.Generic;
using UnityEngine;

namespace Archer
{
    /// <summary>
    /// 방 프리팹이 구운 라이트맵을 들고 다니게 한다.
    /// 유니티는 라이트맵을 '씬'에만 저장하므로, 씬에서 구운 뒤 렌더러별 라이트맵 번호·위치와
    /// 라이트맵 텍스처를 여기에 기록해두고, 방이 생길 때 다시 붙인다.
    /// (기록은 에디터에서 굽기가 끝나면 자동으로 됨 — RoomLightmapBaker)
    /// </summary>
    [DisallowMultipleComponent]
    public class RoomLightmap : MonoBehaviour
    {
        [System.Serializable]
        public struct Entry
        {
            public Renderer renderer;
            public int lightmapIndex;      // 아래 lightmaps 배열 안의 번호
            public Vector4 scaleOffset;
        }

        public Texture2D[] lightmaps;       // 색
        public Texture2D[] directionMaps;   // (Directional 모드일 때만)
        public Entry[] entries;

        void Awake() => Apply();

        public void Apply()
        {
            if (entries == null || entries.Length == 0 || lightmaps == null || lightmaps.Length == 0) return;

            // 현재 씬 라이트맵 뒤에 이 방 라이트맵을 붙인다 (이미 있으면 재사용)
            var current = new List<LightmapData>(LightmapSettings.lightmaps);
            var map = new int[lightmaps.Length];
            for (int i = 0; i < lightmaps.Length; i++)
            {
                int found = current.FindIndex(d => d.lightmapColor == lightmaps[i]);
                if (found < 0)
                {
                    current.Add(new LightmapData
                    {
                        lightmapColor = lightmaps[i],
                        lightmapDir = directionMaps != null && i < directionMaps.Length ? directionMaps[i] : null,
                    });
                    found = current.Count - 1;
                }
                map[i] = found;
            }
            LightmapSettings.lightmaps = current.ToArray();

            foreach (var e in entries)
            {
                if (!e.renderer || e.lightmapIndex < 0 || e.lightmapIndex >= map.Length) continue;
                e.renderer.lightmapIndex = map[e.lightmapIndex];
                e.renderer.lightmapScaleOffset = e.scaleOffset;
            }
        }

#if UNITY_EDITOR
        /// <summary>지금 씬에 구워진 상태를 이 방에 기록 (에디터 전용)</summary>
        public void Capture()
        {
            var sceneMaps = LightmapSettings.lightmaps;
            var used = new List<int>();
            var list = new List<Entry>();
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.lightmapIndex < 0 || r.lightmapIndex >= sceneMaps.Length) continue;
                if (r.receiveGI == ReceiveGI.LightProbes) continue; // 라이트맵 안 받는 렌더러
                int local = used.IndexOf(r.lightmapIndex);
                if (local < 0) { used.Add(r.lightmapIndex); local = used.Count - 1; }
                list.Add(new Entry { renderer = r, lightmapIndex = local, scaleOffset = r.lightmapScaleOffset });
            }
            lightmaps = new Texture2D[used.Count];
            directionMaps = new Texture2D[used.Count];
            for (int i = 0; i < used.Count; i++)
            {
                lightmaps[i] = sceneMaps[used[i]].lightmapColor;
                directionMaps[i] = sceneMaps[used[i]].lightmapDir;
            }
            entries = list.ToArray();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}

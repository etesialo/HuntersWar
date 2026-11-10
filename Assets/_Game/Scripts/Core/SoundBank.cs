using UnityEngine;

namespace Archer
{
    /// <summary>
    /// 소리 묶음 (Data/SoundBank). 소리마다 여러 클립을 넣으면 번갈아 무작위로 난다.
    /// minInterval: 같은 소리가 너무 촘촘히 겹치지 않게 (화살 수십 발이 동시에 맞을 때 등)
    /// </summary>
    [CreateAssetMenu(menuName = "Archer/Sound Bank", fileName = "SoundBank")]
    public class SoundBank : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public Sfx id;
            public AudioClip[] clips;
            [Range(0f, 1.5f)] public float volume = 0.8f;
            [Range(0f, 0.3f)] public float pitchJitter = 0.06f;
            public float minInterval = 0.04f;
        }

        [Range(0f, 1f)] public float sfxVolume = 1f;
        [Range(0f, 1f)] public float bgmVolume = 0.45f;
        public AudioClip bgmBattle;
        public AudioClip bgmBoss;
        public AudioClip bgmLobby;
        public Entry[] entries;
    }
}

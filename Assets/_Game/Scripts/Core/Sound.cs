using System.Collections.Generic;
using UnityEngine;

namespace Archer
{
    /// <summary>게임에서 나는 소리 종류</summary>
    public enum Sfx
    {
        Shoot, ShootFire, ShootFrost, ShootPoison,
        Hit, HitFire, HitFrost, HitPoison,
        Wall, WallFire, WallFrost, WallPoison,
        EnemySpawn, EnemyDie, EnemyShoot, EnemyBulletHit, EnemyCharge,
        PlayerHurt, Heal,
        Coin, Gem,
        LevelUp, SkillPick, Button,
        BattleStart, BossStart, RoomClear, GameOver, StageClear,
        Freeze, IceBreak,
        // 새 항목은 맨 뒤에 (SoundBank 에 번호로 저장됨). 클립이 없으면 소리 없이 넘어감
        Zap, Stun,
    }

    /// <summary>
    /// 소리 재생기. GameManager 가 하나 만든다. 어디서든 Sound.Play(Sfx.Hit) 로 부른다.
    /// 효과음은 AudioSource 16개를 돌려 쓰고, 배경음은 두 개로 부드럽게 교차한다.
    /// </summary>
    public class Sound : MonoBehaviour
    {
        static Sound I;

        SoundBank bank;
        readonly Dictionary<Sfx, SoundBank.Entry> map = new();
        readonly Dictionary<Sfx, float> lastPlay = new();
        AudioSource[] voices;
        int next;
        AudioSource bgmA, bgmB;
        Coroutine fade;

        public static void Create(SoundBank bank)
        {
            if (!bank) return;
            var go = new GameObject("Sound");
            I = go.AddComponent<Sound>();
            I.Init(bank);
        }

        void Init(SoundBank b)
        {
            bank = b;
            if (b.entries != null)
                foreach (var e in b.entries)
                    if (e != null && e.clips != null && e.clips.Length > 0) map[e.id] = e;
            voices = new AudioSource[16];
            for (int i = 0; i < voices.Length; i++) voices[i] = NewSource(false);
            bgmA = NewSource(true);
            bgmB = NewSource(true);
        }

        AudioSource NewSource(bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            return s;
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        /// <summary>효과음. volume 은 추가 배율</summary>
        public static void Play(Sfx id, float volume = 1f)
        {
            if (!I || !I.map.TryGetValue(id, out var e)) return;
            float now = Time.unscaledTime;
            if (I.lastPlay.TryGetValue(id, out float t) && now - t < e.minInterval) return;
            I.lastPlay[id] = now;
            var clip = e.clips[Random.Range(0, e.clips.Length)];
            if (!clip) return;
            var src = I.voices[I.next];
            I.next = (I.next + 1) % I.voices.Length;
            src.pitch = 1f + Random.Range(-e.pitchJitter, e.pitchJitter);
            src.PlayOneShot(clip, e.volume * volume * I.bank.sfxVolume);
        }

        public enum Music { None, Battle, Boss, Lobby }

        /// <summary>배경음 바꾸기 (같은 곡이면 그대로)</summary>
        public static void PlayMusic(Music m)
        {
            if (!I) return;
            var clip = m switch
            {
                Music.Battle => I.bank.bgmBattle,
                Music.Boss => I.bank.bgmBoss,
                Music.Lobby => I.bank.bgmLobby,
                _ => null,
            };
            if (I.bgmA.clip == clip && I.bgmA.isPlaying) return;
            if (I.fade != null) I.StopCoroutine(I.fade);
            I.fade = I.StartCoroutine(I.CrossFade(clip, 0.8f));
        }

        System.Collections.IEnumerator CrossFade(AudioClip clip, float time)
        {
            // A 는 지금 곡, B 로 새 곡을 올리고 끝나면 둘을 바꾼다
            (bgmA, bgmB) = (bgmB, bgmA);
            bgmA.clip = clip;
            bgmA.volume = 0f;
            if (clip) bgmA.Play();
            float from = bgmB.volume, target = bank.bgmVolume;
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                float k = t / time;
                bgmA.volume = target * k;
                bgmB.volume = from * (1f - k);
                yield return null;
            }
            bgmA.volume = clip ? target : 0f;
            bgmB.Stop();
            bgmB.volume = 0f;
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Archer
{
    public enum RoomType { Normal, MiniBoss, Angel, Boss }

    /// <summary>방 순서 진행, 적 스폰, 클리어 판정, 문 열기.</summary>
    public class StageManager : MonoBehaviour
    {
        /// <summary>플레이 영역(안쪽 16 x 26칸) 반폭/반길이</summary>
        public const float HalfW = RoomGrid.InnerHalfW;   // 6
        public const float HalfL = RoomGrid.InnerHalfL;   // 9.75

        public readonly List<Enemy> Enemies = new();
        public int RoomIndex { get; private set; }
        /// <summary>방 프리팹이 있으면 그걸, 없으면 배치도(RoomLayouts.Chapter1)로 만든다.</summary>
        bool UsePrefabs => config.rooms != null && config.rooms.Length > 0;
        public int RoomCount => UsePrefabs ? config.rooms.Length : RoomLayouts.Chapter1.Length;
        public RoomType CurrentType { get; private set; }
        public bool Cleared { get; private set; }

        GameConfig config;
        PlayerController player;
        GameObject roomRoot, doorBlock, portal;
        ExitBarrier barrier;
        Transform exitPoint;
        bool busy;
        int wave;                   // 테스트 반복 모드: 지금 몇 번째 웨이브인지
        int Level => config.testLoopRoom ? wave : RoomIndex;
        /// <summary>테스트 반복 모드의 웨이브 번호 (0부터)</summary>
        public int Wave => wave;
        /// <summary>이번 런에서 도달한 스테이지 (기록용, 1부터)</summary>
        public int Progress => config != null && config.testLoopRoom ? wave + 1 : RoomIndex + 1;

        public void Init(GameConfig cfg, PlayerController p)
        {
            config = cfg;
            player = p;
        }

        public void Begin()
        {
            // 편집하려고 씬에 올려둔 방은 치운다 (게임이 직접 방을 만든다)
            foreach (var r in FindObjectsByType<RoomRoot>(FindObjectsSortMode.None))
                Destroy(r.gameObject);
            wave = 0;
            StartCoroutine(EnterRoom(0, false));
        }

        public static Vector3 ClampToRoom(Vector3 p, float margin)
        {
            p.x = Mathf.Clamp(p.x, -HalfW + margin, HalfW - margin);
            p.z = Mathf.Clamp(p.z, -HalfL + margin, HalfL - margin);
            return p;
        }

        IEnumerator EnterRoom(int index, bool fade)
        {
            busy = true;
            var gm = GameManager.I;
            if (fade) yield return gm.ui.Fade(1f, 0.3f);

            Cleanup();
            RoomIndex = index;
            Cleared = false;

            RoomRoot room;
            if (UsePrefabs)
            {
                room = Instantiate(config.rooms[index], Vector3.zero, Quaternion.identity);
            }
            else
            {
                var def = RoomLayouts.Chapter1[index];
                room = RoomBuilder.Build(config, def.layout, def.type, def.name);
            }
            roomRoot = room.gameObject;
            if (config.ambience) Instantiate(config.ambience, roomRoot.transform);
            if (!config.autoObstacles)
            {
                // 예전에 자동 생성돼 방 프리팹에 남은 바위 장애물은 끈다 (프리팹은 그대로 둔다)
                var obs = room.transform.Find("Obstacles");
                if (obs)
                    foreach (Transform o in obs)
                        if (o.name == "Obstacle") o.gameObject.SetActive(false);
            }
            doorBlock = room.door;
            exitPoint = room.exit;
            portal = room.portal;
            if (portal)
            {
                portal.SetActive(false);
                // 출구 표시(초록 판)는 게임 화면에 보이지 않게. 출구 판정은 위치로만 한다
                foreach (var r in portal.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            }
            // 입구/출구 표식에 혹시 붙은 그림도 끔
            foreach (var marker in new[] { room.entry, room.exit })
                if (marker)
                    foreach (var r in marker.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            if (config.exitBarrier && room.exit)
            {
                barrier = Instantiate(config.exitBarrier, roomRoot.transform);
                barrier.Place(room.transform, room.exit.localPosition);
            }
            CurrentType = room.type;
            if (room.entry)
            {
                var sp = room.transform.TransformPoint(RoomBuilder.EntrySpawnPoint(room.entry.localPosition));
                player.Teleport(new Vector3(sp.x, 0f, sp.z));
            }
            else player.Teleport(new Vector3(0f, 0f, -HalfL + 1.5f));
            gm.cam.Snap();
            gm.ui.SetRoom(index + 1, RoomCount, CurrentType);

            if (fade) yield return gm.ui.Fade(0f, 0.3f);

            if (CurrentType == RoomType.Angel)
            {
                Instantiate(config.angel, new Vector3(0f, 0f, 2f), Quaternion.Euler(0f, 180f, 0f), roomRoot.transform);
                Cleared = true;
                OpenDoor();
                gm.ui.Toast("천사가 나타났다!");
            }
            else
            {
                if (CurrentType == RoomType.Boss) gm.ui.Toast("보스 등장!");
                else if (CurrentType == RoomType.MiniBoss) gm.ui.Toast("강력한 적이 나타났다!");
                BattleSound(CurrentType);

                foreach (var sp in room.Spawns)
                {
                    var prefab = config.GetEnemy((char)sp.kind);
                    if (prefab != null) SpawnEnemy(prefab, sp.transform.position);
                }
                if (Enemies.Count == 0) OnRoomCleared();
            }

            busy = false;
            gm.OnRoomEntered(index);
        }

        public Enemy SpawnEnemy(Enemy prefab, Vector3 pos)
        {
            pos = ClampToRoom(pos, 0.6f);
            pos.y = 0f;
            var e = Instantiate(prefab, pos, Quaternion.Euler(0f, 180f, 0f), roomRoot.transform);
            float hpMul = 1f + config.hpScalePerRoom * Level;
            float dmgMul = 1f + config.dmgScalePerRoom * Level;
            e.Init(this, player, hpMul, dmgMul);
            Enemies.Add(e);
            return e;
        }

        public void OnEnemyDied(Enemy e)
        {
            Enemies.Remove(e);
            if (!Cleared && !busy && Enemies.Count == 0) OnRoomCleared();
        }

        void OnRoomCleared()
        {
            Cleared = true;
            foreach (var p in Pickup.Active.ToArray()) p.StartMagnet();
            Projectile.DespawnAll(true);
            Sound.Play(Sfx.RoomClear);
            Sound.PlayMusic(Sound.Music.Battle);

            if (config.testLoopRoom)
            {
                StartCoroutine(NextWave());
                return;
            }
            if (CurrentType == RoomType.Boss)
            {
                StartCoroutine(FinishStage());
                return;
            }
            OpenDoor();
            GameManager.I.ui.Toast("클리어!");
        }

        /// <summary>
        /// 테스트 반복 모드: 같은 방에 다음 방의 적 구성을 웨이브로 내보낸다.
        /// 보석을 다 줍고 레벨업 선택이 끝난 뒤에 나온다 (선택 중엔 timeScale 0 이라 기다림도 멈춤).
        /// </summary>
        IEnumerator NextWave()
        {
            busy = true;
            var gm = GameManager.I;
            gm.ui.Toast($"웨이브 {wave + 1} 클리어!");
            yield return new WaitForSeconds(1.2f);
            for (float t = 0f; t < 6f && Pickup.Active.Count > 0; t += Time.deltaTime) yield return null;
            while (gm.State != GameState.Playing)
            {
                if (player.IsDead) yield break;
                yield return null;
            }
            yield return new WaitForSeconds(0.6f);
            if (player.IsDead || !roomRoot) yield break;

            wave++;
            Cleared = false;
            var list = WaveSpawns(wave, out var type);
            var here = roomRoot.transform;
            BattleSound(type);
            if (type == RoomType.Boss) gm.ui.Toast("보스 등장!");
            else if (type == RoomType.MiniBoss) gm.ui.Toast("강력한 적이 나타났다!");
            else gm.ui.Toast($"웨이브 {wave + 1}");
            foreach (var (kind, local) in list)
            {
                var prefab = config.GetEnemy((char)kind);
                if (prefab != null) SpawnEnemy(prefab, here.TransformPoint(local));
            }
            gm.ui.SetRoom(wave + 1, RoomCount, type);
            busy = false;
            if (Enemies.Count == 0) OnRoomCleared();
        }

        /// <summary>
        /// 웨이브 구성 (적 종류 + 방 기준 위치).
        /// 첫 바퀴(방 수만큼)는 방 프리팹 순서대로, 그 뒤로는 모든 방의 일반 몬스터를 섞어 무작위로 뽑는다.
        /// 섞는 구간에선 5번째마다 중간 보스, 10번째마다 보스 방 구성.
        /// </summary>
        List<(EnemyKind kind, Vector3 local)> WaveSpawns(int n, out RoomType type)
        {
            var result = new List<(EnemyKind, Vector3)>();
            type = RoomType.Normal;
            if (!UsePrefabs)
            {
                var cur = roomRoot.GetComponent<RoomRoot>();
                foreach (var sp in cur.Spawns) result.Add((sp.kind, cur.transform.InverseTransformPoint(sp.transform.position)));
                return result;
            }
            int count = config.rooms.Length;

            // 첫 바퀴: 방 순서대로 (적이 없는 천사 방 등은 건너뜀)
            if (n < count)
            {
                for (int i = 0; i < count; i++)
                {
                    var r = config.rooms[(n + i) % count];
                    if (!r || r.Spawns.Length == 0) continue;
                    type = r.type;
                    foreach (var sp in r.Spawns) result.Add((sp.kind, r.transform.InverseTransformPoint(sp.transform.position)));
                    return result;
                }
                return result;
            }

            // 보스 / 중간 보스 웨이브: 그 종류의 방 구성을 그대로
            int cycle = n % 10;
            var want = cycle == 9 ? RoomType.Boss : cycle == 4 ? RoomType.MiniBoss : RoomType.Normal;
            if (want != RoomType.Normal)
                foreach (var r in config.rooms)
                    if (r && r.type == want && r.Spawns.Length > 0)
                    {
                        type = want;
                        foreach (var sp in r.Spawns) result.Add((sp.kind, r.transform.InverseTransformPoint(sp.transform.position)));
                        return result;
                    }

            // 일반 웨이브: 모든 방의 일반 몬스터 자리에서 무작위로 섞어 뽑기
            var pool = new List<(EnemyKind, Vector3)>();
            foreach (var r in config.rooms)
            {
                if (!r || r.type == RoomType.Boss || r.type == RoomType.MiniBoss) continue;
                foreach (var sp in r.Spawns)
                {
                    var prefab = config.GetEnemy((char)sp.kind);
                    if (prefab == null || prefab.isBoss || prefab is EnemyMiniBoss || prefab is EnemyBoss) continue;
                    pool.Add((sp.kind, r.transform.InverseTransformPoint(sp.transform.position)));
                }
            }
            if (pool.Count == 0) return result;
            int want2 = Mathf.Min(12, 4 + (n - count) / 3);
            var used = new HashSet<int>();
            for (int i = 0; i < want2; i++)
            {
                int pick = Random.Range(0, pool.Count);
                // 같은 자리 겹침은 피하고, 자리가 모자라면 살짝 비켜 놓는다
                for (int tries = 0; tries < 6 && used.Contains(pick); tries++) pick = Random.Range(0, pool.Count);
                var (kind, pos) = pool[pick];
                if (used.Contains(pick)) pos += new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f));
                used.Add(pick);
                result.Add((kind, pos));
            }
            return result;
        }

        IEnumerator FinishStage()
        {
            GameManager.I.ui.Toast("보스 처치!");
            yield return new WaitForSeconds(2f);
            GameManager.I.StageClear();
        }

        /// <summary>싸움 시작 소리 + 보스면 보스 음악</summary>
        static void BattleSound(RoomType type)
        {
            bool boss = type == RoomType.Boss || type == RoomType.MiniBoss;
            Sound.Play(boss ? Sfx.BossStart : Sfx.BattleStart);
            Sound.PlayMusic(boss ? Sound.Music.Boss : Sound.Music.Battle);
        }

        void OpenDoor()
        {
            if (doorBlock) doorBlock.SetActive(false);
            if (portal) portal.SetActive(true);
            if (barrier) barrier.Open();
        }

        void Cleanup()
        {
            Enemies.Clear();
            if (roomRoot)
            {
                roomRoot.SetActive(false);
                Destroy(roomRoot);
            }
            Projectile.DespawnAll(false);
            Pickup.DespawnAll();
            GameManager.I.ui.HideBossBar();
        }

        void Update()
        {
            if (busy || !Cleared || player == null || player.IsDead || config.testLoopRoom) return;
            if (CurrentType == RoomType.Boss || RoomIndex >= RoomCount - 1) return;
            if (GameManager.I.State != GameState.Playing) return;

            var p = player.transform.position;
            bool atExit = exitPoint
                ? new Vector2(p.x - exitPoint.position.x, p.z - exitPoint.position.z).magnitude < RoomGrid.Cell * 1.4f
                : p.z > HalfL - 0.25f && Mathf.Abs(p.x) < RoomBuilder.DoorW * 0.5f + 0.2f;
            if (atExit)
                StartCoroutine(EnterRoom(RoomIndex + 1, true));
        }
    }
}

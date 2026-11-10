using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Archer
{
    public enum GameState { Playing, Choice, Paused, GameOver, Clear, Lobby, Result }

    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        public GameConfig config;
        public UIManager ui;
        public StageManager stage;
        public CameraFollow cam;
        public VirtualJoystick joystick;

        public PlayerController Player { get; private set; }
        public GameState State { get; private set; } = GameState.Playing;
        public int Coins { get; private set; }
        public int Kills { get; set; }

        int pendingLevelUps;
        bool revived;
        /// <summary>다음 씬 로드 때 로비를 건너뛰고 바로 전투</summary>
        static bool skipLobbyOnce;

        void Awake()
        {
            I = this;
            Pool.Reset();
            Projectile.Active.Clear();
            Pickup.Active.Clear();
            Time.timeScale = 1f;
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            Sound.Create(config.sounds);
        }

        void Start()
        {
            Player = Instantiate(config.player, new Vector3(0f, 0f, -8f), Quaternion.identity);
            Player.Init(joystick);
            Player.GodMode = config.testGodMode; // 스킬/무적 조절은 에디터 메뉴 Archer/테스트 패널 (별도 창)
            cam.target = Player.transform;
            ui.Bind(this);
            stage.Init(config, Player);
            RefreshHud();
            bool lobby = !skipLobbyOnce && ui.HasLobby;
            skipLobbyOnce = false;
            if (lobby)
            {
                SetState(GameState.Lobby);
                Time.timeScale = 1f;
                ui.ShowLobby(SaveData.Gold, SaveData.BestStage, SaveData.TotalKills);
                Sound.PlayMusic(Sound.Music.Lobby);
            }
            else BeginRun();
            StartCoroutine(ui.Fade(0f, 0.5f));
        }

        /// <summary>로비의 전투 시작 버튼</summary>
        public void StartRun()
        {
            if (State != GameState.Lobby) return;
            StartCoroutine(StartRunRoutine());
        }

        IEnumerator StartRunRoutine()
        {
            yield return ui.Fade(1f, 0.25f);
            ui.HideLobby();
            BeginRun();
            yield return ui.Fade(0f, 0.35f);
        }

        void BeginRun()
        {
            SetState(GameState.Playing);
            stage.Begin();
            Sound.PlayMusic(Sound.Music.Battle);
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        void SetState(GameState s)
        {
            State = s;
            Time.timeScale = s == GameState.Playing ? 1f : 0f;
        }

        void Resume()
        {
            SetState(GameState.Playing);
            TryLevelUp();
        }

        public void RefreshHud()
        {
            ui.SetLevel(Player.Level, Player.Exp, Player.ExpToNext);
            ui.SetCoins(Coins);
        }

        // ───────── 방 / 보상 ─────────

        public void OnRoomEntered(int index) { }

        public void Collect(PickupType type, int value)
        {
            if (type == PickupType.Exp)
            {
                Player.AddExp(value);
            }
            else
            {
                Coins += value;
                ui.SetCoins(Coins);
            }
        }

        public void OnLevelUp()
        {
            pendingLevelUps++;
            TryLevelUp();
        }

        void TryLevelUp()
        {
            if (State != GameState.Playing || pendingLevelUps <= 0 || Player.IsDead) return;
            pendingLevelUps--;
            ShowSkillChoice($"레벨 업!  Lv.{Player.Level}");
        }

        public void ShowSkillChoice(string title)
        {
            var options = SkillDatabase.Roll(Player.Skills, 3, Player);
            if (options.Count == 0)
            {
                Resume();
                return;
            }
            SetState(GameState.Choice);
            Sound.Play(Sfx.LevelUp);
            ui.ShowSkillSelect(title, options, Player.Skills, id =>
            {
                Sound.Play(Sfx.SkillPick);
                Player.ApplySkill(id);
                ui.HideSkillSelect();
                ui.Toast(SkillDatabase.Get(id).name);
                Resume();
            });
        }

        public void OnAngelTouched(Angel angel)
        {
            SetState(GameState.Choice);
            ui.ShowAngel(
                () =>
                {
                    Player.Heal(Player.MaxHp * 0.4f);
                    angel.Vanish();
                    Resume();
                },
                () =>
                {
                    angel.Vanish();
                    ShowSkillChoice("천사의 축복");
                });
        }

        // ───────── 사망 / 클리어 ─────────

        public void OnPlayerDied()
        {
            SetState(GameState.GameOver);
            Sound.Play(Sfx.GameOver);
            Sound.PlayMusic(Sound.Music.None);
            StartCoroutine(GameOverRoutine());
        }

        IEnumerator GameOverRoutine()
        {
            yield return new WaitForSecondsRealtime(0.8f);
            if (State == GameState.GameOver) ui.ShowGameOver(!revived); // 그 사이 포기했으면 안 띄움
        }

        public void Revive()
        {
            if (revived || State != GameState.GameOver) return;
            revived = true;
            ui.HideGameOver();
            Projectile.DespawnAll(true);
            Player.Revive();
            Sound.Play(Sfx.Heal);
            Sound.PlayMusic(Sound.Music.Battle);
            Resume();
        }

        public void StageClear()
        {
            Sound.Play(Sfx.StageClear);
            ShowResult(true);
        }

        /// <summary>게임오버 창의 "포기": 결과 화면으로</summary>
        public void GiveUp()
        {
            if (State != GameState.GameOver) return;
            ui.HideGameOver();
            ShowResult(false);
        }

        /// <summary>런 결과: 기록 저장 + 결과 화면</summary>
        void ShowResult(bool clear)
        {
            SetState(GameState.Result);
            Sound.PlayMusic(Sound.Music.None);
            int reached = stage ? stage.Progress : 0;
            bool best = SaveData.Commit(reached, Kills, Coins);
            if (ui.HasResult) ui.ShowResult(clear, reached, Kills, Coins, Player.Level, Player.Skills, best);
            else ui.ShowClear($"처치한 적   {Kills}\n획득 코인   {Coins}\n최종 레벨   Lv.{Player.Level}");
        }

        /// <summary>결과 화면: 바로 다시 도전</summary>
        public void Retry()
        {
            skipLobbyOnce = true;
            Restart();
        }

        /// <summary>결과 화면: 로비로</summary>
        public void ToLobby()
        {
            skipLobbyOnce = false;
            Restart();
        }

        // ───────── 일시정지 ─────────

        public void Pause()
        {
            if (State != GameState.Playing) return;
            SetState(GameState.Paused);
            Sound.Play(Sfx.Button);
            ui.ShowPause(SkillSummary());
        }

        public void ResumeFromPause()
        {
            if (State != GameState.Paused) return;
            ui.HidePause();
            Sound.Play(Sfx.Button);
            Resume();
        }

        public void Restart()
        {
            if (State != GameState.Lobby && State != GameState.Result) skipLobbyOnce = true; // 일시정지의 처음부터는 바로 다시
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        string SkillSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"공격력 {Player.Attack:0}   체력 {Mathf.CeilToInt(Player.Hp)}/{Mathf.CeilToInt(Player.MaxHp)}");
            sb.AppendLine();
            if (Player.Skills.Count == 0)
            {
                sb.Append("아직 획득한 스킬이 없습니다");
            }
            else
            {
                foreach (var kv in Player.Skills)
                {
                    var def = SkillDatabase.Get(kv.Key);
                    sb.AppendLine(def.maxLevel >= 99 ? def.name : $"{def.name}  Lv.{kv.Value}");
                }
            }
            return sb.ToString();
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Archer
{
    [DefaultExecutionOrder(100)]
    public class UIManager : MonoBehaviour
    {
        public Canvas canvas;
        [Tooltip("UI 그림 (스킬 아이콘, 스테이지 진행 점). 비면 예전 단색 UI")]
        public UiSkin skin;
        [Tooltip("스테이지 진행 점을 놓을 자리 (비면 진행 점 없음)")]
        public RectTransform stageDots;

        [Header("HUD")]
        public Text levelText;
        public RectTransform expFill;
        public Text roomText;
        public Text coinText;
        public Text toastText;
        public Button pauseButton;

        [Header("보스 체력바")]
        public GameObject bossBar;
        public Text bossName;
        public RectTransform bossFill;

        [Header("월드 UI")]
        public RectTransform worldLayer;
        public HpBar hpBarTemplate;
        public Text popupTemplate;

        [Header("스킬 선택")]
        public GameObject skillPanel;
        public Text skillTitle;
        public SkillCard[] skillCards;

        [Header("천사")]
        public GameObject angelPanel;
        public Button angelHealButton;
        public Button angelSkillButton;

        [Header("게임 오버")]
        public GameObject gameOverPanel;
        public Button reviveButton;
        public Button gameOverRetryButton;

        [Header("클리어")]
        public GameObject clearPanel;
        public Text clearStats;
        public Button clearRetryButton;

        [Header("일시정지")]
        public GameObject pausePanel;
        public Text pauseSkills;
        public Button resumeButton;
        public Button pauseRetryButton;

        [Header("로비")]
        public GameObject lobbyPanel;
        public Text lobbyGold;
        public Text lobbyRecord;
        public Button lobbyStartButton;

        [Header("결과")]
        public GameObject resultPanel;
        public Text resultTitle;
        public Text resultStats;
        public GameObject resultNewBest;
        public RectTransform resultSkills;
        public Button resultRetryButton;
        public Button resultLobbyButton;

        [Header("기타")]
        public Image fade;

        public bool HasLobby => lobbyPanel != null;
        public bool HasResult => resultPanel != null;

        class Popup
        {
            public Text text;
            public RectTransform rt;
            public Vector3 world;
            public Color color;
            public float t, scale;
        }

        const float PopupDuration = 0.7f;

        Camera cam;
        RectTransform barLayer, popupLayer;
        readonly List<Popup> popups = new();
        readonly Stack<Popup> freePopups = new();
        Coroutine toastRoutine;

        void Awake()
        {
            cam = canvas != null && canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            barLayer = MakeLayer("Bars");
            popupLayer = MakeLayer("Popups");
            MakeVignette();

            foreach (var p in new[] { skillPanel, angelPanel, gameOverPanel, clearPanel, pausePanel, lobbyPanel, resultPanel })
                if (p) p.SetActive(false);
            if (bossBar) bossBar.SetActive(false);
            if (toastText) toastText.gameObject.SetActive(false);
            if (fade)
            {
                fade.gameObject.SetActive(true);
                SetFadeAlpha(1f);
                fade.raycastTarget = true;
            }
        }

        // ───────── 피격 테두리 ─────────

        Image vignette;
        float hurtT;
        bool danger;

        /// <summary>화면 가장자리가 붉게 물드는 그림 (가운데 투명). 텍스처는 코드로 만든다</summary>
        void MakeVignette()
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx * 0.8f + dy * dy * 0.8f);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.1f, r));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            var go = new GameObject("HurtVignette", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            // 게임 화면(체력바/데미지 숫자) 위, HUD·창 아래 → HUD 가 붉게 물들지 않게
            if (worldLayer) rt.SetSiblingIndex(worldLayer.GetSiblingIndex() + 1);
            vignette = go.GetComponent<Image>();
            vignette.sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            vignette.raycastTarget = false;
            vignette.color = new Color(0.9f, 0.05f, 0.05f, 0f);
        }

        /// <summary>플레이어가 맞음: 붉은 테두리가 번쩍</summary>
        public void HurtFlash() => hurtT = 1f;

        /// <summary>체력이 낮음: 붉은 테두리가 천천히 맥박</summary>
        public void SetDanger(bool on) => danger = on;

        void UpdateVignette()
        {
            if (!vignette) return;
            hurtT = Mathf.Max(0f, hurtT - Time.unscaledDeltaTime / 0.35f);
            float a = hurtT * 0.9f;
            if (danger) a = Mathf.Max(a, 0.35f + 0.2f * Mathf.Sin(Time.unscaledTime * 5f));
            var c = vignette.color;
            c.a = a;
            vignette.color = c;
        }

        RectTransform MakeLayer(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(worldLayer, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public void Bind(GameManager gm)
        {
            pauseButton.onClick.AddListener(gm.Pause);
            resumeButton.onClick.AddListener(gm.ResumeFromPause);
            pauseRetryButton.onClick.AddListener(gm.Restart);
            reviveButton.onClick.AddListener(gm.Revive);
            // 결과 화면이 있으면 게임오버의 두 번째 버튼은 "포기 → 결과"
            gameOverRetryButton.onClick.AddListener(HasResult ? gm.GiveUp : gm.Restart);
            clearRetryButton.onClick.AddListener(gm.Restart);
            if (lobbyStartButton) lobbyStartButton.onClick.AddListener(gm.StartRun);
            if (resultRetryButton) resultRetryButton.onClick.AddListener(gm.Retry);
            if (resultLobbyButton) resultLobbyButton.onClick.AddListener(gm.ToLobby);
            // 모든 버튼 클릭음 (스킬 카드는 고르는 소리가 따로 남)
            foreach (var b in GetComponentsInChildren<Button>(true))
                if (!b.GetComponent<SkillCard>() && b != pauseButton && b != resumeButton)
                    b.onClick.AddListener(() => Sound.Play(Sfx.Button));
        }

        /// <summary>
        /// 월드 위치 → worldLayer 기준 UI 좌표.
        /// 캔버스(Screen Space - Camera)는 프레임 끝에야 카메라를 따라 옮겨지므로, 캔버스 위치로 변환하면
        /// 카메라가 움직이는 동안 한 프레임씩 어긋나 체력바가 튄다. 그래서 화면 비율(뷰포트)로 바로 계산한다.
        /// (worldLayer 는 캔버스 전체 = 카메라 화면 전체를 덮는다)
        /// </summary>
        public bool WorldToLocal(Vector3 world, out Vector2 local)
        {
            // UI(Canvas)가 꺼져 있으면 초기화가 안 돼 있다 → 매 프레임 에러가 쌓여 에디터가 멈추지 않게 그냥 건너뜀
            if (cam == null || worldLayer == null)
            {
                local = default;
                return false;
            }
            var vp = cam.WorldToViewportPoint(world);
            if (vp.z < 0f)
            {
                local = default;
                return false;
            }
            var r = worldLayer.rect;
            local = new Vector2(r.x + vp.x * r.width, r.y + vp.y * r.height);
            return true;
        }

        // ───────── HUD ─────────

        public void SetLevel(int level, int exp, int need)
        {
            levelText.text = $"Lv.{level}";
            expFill.anchorMax = new Vector2(Mathf.Clamp01(exp / (float)need), 1f);
        }

        public void SetRoom(int cur, int total, RoomType type)
        {
            string tag = type switch
            {
                RoomType.Boss => "  보스",
                RoomType.MiniBoss => "  중간 보스",
                RoomType.Angel => "  천사",
                _ => "",
            };
            roomText.text = $"스테이지 {cur} / {total}{tag}";
            UpdateDots(cur, total);
        }

        Image[] dots;

        /// <summary>스테이지 진행 점: 지난 방 = 체크, 지금 방 = 빛나는 금색, 앞으로 = 어둡게, 마지막 = 보스</summary>
        void UpdateDots(int cur, int total)
        {
            if (!stageDots || !skin || !skin.nodeDone || total <= 0) return;
            if (dots == null || dots.Length != total)
            {
                foreach (Transform c in stageDots) Destroy(c.gameObject);
                dots = new Image[total];
                float w = stageDots.rect.width, size = Mathf.Min(stageDots.rect.height, w / total * 0.8f);
                for (int i = 0; i < total; i++)
                {
                    var go = new GameObject("Dot" + i, typeof(RectTransform), typeof(Image));
                    var rt = (RectTransform)go.transform;
                    rt.SetParent(stageDots, false);
                    rt.anchorMin = rt.anchorMax = new Vector2(total == 1 ? 0.5f : i / (float)(total - 1), 0.5f);
                    rt.sizeDelta = new Vector2(size, size);
                    dots[i] = go.GetComponent<Image>();
                    dots[i].raycastTarget = false;
                    dots[i].preserveAspect = true;
                }
            }
            // 테스트 반복 모드에선 웨이브가 방 수를 넘을 수 있다 → 한 바퀴 돌면 처음부터
            int at = (cur - 1) % total;
            for (int i = 0; i < total; i++)
            {
                bool boss = i == total - 1 && skin.nodeBoss;
                var img = dots[i];
                if (i < at) { img.sprite = skin.nodeDone; img.color = Color.white; }
                else if (i == at) { img.sprite = boss ? skin.nodeBoss : skin.nodeCurrent; img.color = Color.white; }
                else { img.sprite = boss ? skin.nodeBoss : skin.nodeCurrent; img.color = skin.nodeUpcoming; }
                img.rectTransform.localScale = Vector3.one * (i == at ? 1.3f : 1f);
            }
        }

        public void SetCoins(int coins) => coinText.text = coins.ToString();

        public void ShowBossBar(string name)
        {
            bossBar.SetActive(true);
            bossName.text = name;
            SetBossHp(1f);
        }

        public void SetBossHp(float ratio) => bossFill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);

        public void HideBossBar()
        {
            if (bossBar) bossBar.SetActive(false);
        }

        public HpBar CreateHpBar(Transform target, float offset, float width, Color color, bool showLabel)
        {
            var bar = Instantiate(hpBarTemplate, barLayer);
            bar.Bind(this, target, offset, width, color, showLabel);
            return bar;
        }

        // ───────── 데미지 숫자 ─────────

        public void ShowDamage(Vector3 world, float amount, bool crit, Color color)
        {
            ShowText(world, Mathf.Max(1, Mathf.RoundToInt(amount)).ToString(), color, crit ? 1.45f : 1f);
        }

        public void ShowText(Vector3 world, string text, Color color, float scale)
        {
            var p = freePopups.Count > 0 ? freePopups.Pop() : NewPopup();
            var jitter = Random.insideUnitSphere * 0.3f;
            jitter.y = 0f;
            p.world = world + jitter;
            p.text.text = text;
            p.color = color;
            p.scale = scale;
            p.t = 0f;
            p.text.gameObject.SetActive(true);
            p.rt.SetAsLastSibling();
            popups.Add(p);
            UpdatePopup(p, 0f);
        }

        Popup NewPopup()
        {
            var t = Instantiate(popupTemplate, popupLayer);
            return new Popup { text = t, rt = t.rectTransform };
        }

        void LateUpdate()
        {
            UpdateVignette();
            float dt = Time.deltaTime;
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var p = popups[i];
                p.t += dt;
                float k = p.t / PopupDuration;
                if (k >= 1f)
                {
                    p.text.gameObject.SetActive(false);
                    popups.RemoveAt(i);
                    freePopups.Push(p);
                    continue;
                }
                UpdatePopup(p, k);
            }
        }

        void UpdatePopup(Popup p, float k)
        {
            if (WorldToLocal(p.world + Vector3.up * (k * 1.2f), out var lp)) p.rt.anchoredPosition = lp;
            float s = k < 0.15f ? Mathf.Lerp(p.scale * 1.6f, p.scale, k / 0.15f) : p.scale;
            p.rt.localScale = Vector3.one * s;
            var c = p.color;
            c.a = k > 0.6f ? 1f - (k - 0.6f) / 0.4f : 1f;
            p.text.color = c;
        }

        // ───────── 패널 ─────────

        void Open(GameObject panel)
        {
            panel.SetActive(true);
            var content = panel.transform.Find("Content");
            if (content) StartCoroutine(Pop(content));
        }

        IEnumerator Pop(Transform t)
        {
            float k = 0f;
            while (k < 1f)
            {
                k += Time.unscaledDeltaTime / 0.2f;
                t.localScale = Vector3.one * Mathf.LerpUnclamped(0.8f, 1f, Combat.EaseOutBack(Mathf.Clamp01(k)));
                yield return null;
            }
            t.localScale = Vector3.one;
        }

        public void ShowSkillSelect(string title, List<SkillDef> options, Dictionary<SkillId, int> levels, System.Action<SkillId> onPick)
        {
            skillTitle.text = title;
            for (int i = 0; i < skillCards.Length; i++)
            {
                var card = skillCards[i];
                if (i >= options.Count)
                {
                    card.gameObject.SetActive(false);
                    continue;
                }
                var def = options[i];
                levels.TryGetValue(def.id, out int lv);
                card.gameObject.SetActive(true);
                card.Set(def, lv, () => onPick(def.id), skin);
            }
            Open(skillPanel);
        }

        public void HideSkillSelect() => skillPanel.SetActive(false);

        public void ShowAngel(System.Action onHeal, System.Action onSkill)
        {
            angelHealButton.onClick.RemoveAllListeners();
            angelSkillButton.onClick.RemoveAllListeners();
            angelHealButton.onClick.AddListener(() => { angelPanel.SetActive(false); onHeal(); });
            angelSkillButton.onClick.AddListener(() => { angelPanel.SetActive(false); onSkill(); });
            Open(angelPanel);
        }

        public void ShowGameOver(bool canRevive)
        {
            reviveButton.gameObject.SetActive(canRevive);
            Open(gameOverPanel);
        }

        public void HideGameOver() => gameOverPanel.SetActive(false);

        public void ShowClear(string stats)
        {
            clearStats.text = stats;
            Open(clearPanel);
        }

        // ───────── 로비 / 결과 ─────────

        public void ShowLobby(int gold, int bestStage, int totalKills)
        {
            if (!lobbyPanel) return;
            lobbyGold.text = gold.ToString("N0");
            lobbyRecord.text = bestStage > 0 ? $"최고 기록  스테이지 {bestStage}     누적 처치  {totalKills:N0}" : "첫 모험을 떠나 보세요!";
            lobbyPanel.SetActive(true);
            SetHud(false);
        }

        public void HideLobby()
        {
            if (lobbyPanel) lobbyPanel.SetActive(false);
            SetHud(true);
        }

        /// <summary>로비에선 전투 HUD(경험치 바, 스테이지 등)를 숨김</summary>
        void SetHud(bool on)
        {
            var hud = canvas ? canvas.transform.Find("HUD") : null;
            if (hud) hud.gameObject.SetActive(on);
        }

        public void ShowResult(bool clear, int stage, int kills, int coins, int level, Dictionary<SkillId, int> skills, bool newBest)
        {
            if (!resultPanel) return;
            resultTitle.text = clear ? "스테이지 클리어!" : "모험 종료";
            resultStats.text =
                $"도달 스테이지   {stage}\n" +
                $"처치한 적   {kills}\n" +
                $"획득 골드   {coins}\n" +
                $"최종 레벨   Lv.{level}";
            if (resultNewBest) resultNewBest.SetActive(newBest);
            // 배운 스킬 아이콘 줄
            if (resultSkills)
            {
                foreach (Transform c in resultSkills) Destroy(c.gameObject);
                const float size = 96f, gap = 10f;
                int i = 0, perRow = 7;
                int count = skills.Count;
                foreach (var kv in skills)
                {
                    var go = new GameObject(kv.Key.ToString(), typeof(RectTransform), typeof(Image));
                    var rt = (RectTransform)go.transform;
                    rt.SetParent(resultSkills, false);
                    int row = i / perRow, col = i % perRow;
                    int inRow = Mathf.Min(perRow, count - row * perRow);
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.sizeDelta = new Vector2(size, size);
                    rt.anchoredPosition = new Vector2((col - (inRow - 1) * 0.5f) * (size + gap), -size * 0.5f - row * (size + gap));
                    var img = go.GetComponent<Image>();
                    img.sprite = skin ? skin.Icon(kv.Key) : null;
                    img.preserveAspect = true;
                    img.raycastTarget = false;
                    if (kv.Value > 1)
                    {
                        var t = Instantiate(resultStats, rt);
                        t.text = kv.Value.ToString();
                        t.fontSize = 34;
                        t.alignment = TextAnchor.LowerRight;
                        t.rectTransform.anchorMin = Vector2.zero;
                        t.rectTransform.anchorMax = Vector2.one;
                        t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
                        t.color = skin ? skin.gold : Color.yellow;
                    }
                    i++;
                }
            }
            SetHud(false);
            Open(resultPanel);
        }

        public void ShowPause(string skills)
        {
            pauseSkills.text = skills;
            Open(pausePanel);
        }

        public void HidePause() => pausePanel.SetActive(false);

        // ───────── 토스트 / 페이드 ─────────

        public void Toast(string msg)
        {
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ToastRoutine(msg));
        }

        IEnumerator ToastRoutine(string msg)
        {
            toastText.text = msg;
            toastText.gameObject.SetActive(true);
            var rt = toastText.rectTransform;
            float t = 0f;
            while (t < 1.4f)
            {
                t += Time.unscaledDeltaTime;
                rt.localScale = Vector3.one * (t < 0.2f ? Mathf.Lerp(0.5f, 1f, t / 0.2f) : 1f);
                var c = toastText.color;
                c.a = t > 1f ? 1f - (t - 1f) / 0.4f : 1f;
                toastText.color = c;
                yield return null;
            }
            toastText.gameObject.SetActive(false);
        }

        public IEnumerator Fade(float to, float duration)
        {
            float from = fade.color.a;
            fade.raycastTarget = true;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                SetFadeAlpha(Mathf.Lerp(from, to, t / duration));
                yield return null;
            }
            SetFadeAlpha(to);
            fade.raycastTarget = to > 0.01f;
        }

        void SetFadeAlpha(float a)
        {
            var c = fade.color;
            c.a = a;
            fade.color = c;
        }
    }
}

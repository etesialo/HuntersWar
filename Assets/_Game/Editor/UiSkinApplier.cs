using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Archer.EditorTools
{
    /// <summary>
    /// 네이비·골드 UI 스킨을 InGame 씬의 UI(GameSetup 이 만든 것)에 입힌다.
    /// 오브젝트를 새로 만들지 않고 그림/색/배치만 바꾼다 (배너·창·진행 점처럼 없는 장식만 추가).
    /// 메뉴: Archer/UI 스킨 적용 (InGame). 다시 눌러도 같은 결과.
    /// </summary>
    public static class UiSkinApplier
    {
        const string ArtDir = "Assets/_Game/Art/UI/";
        const string SkinPath = "Assets/_Game/Data/UiSkin.asset";
        const string ScenePath = "Assets/_Game/Scenes/InGame.unity";

        [MenuItem("Archer/UI 스킨 적용 (InGame)")]
        public static void ApplyMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("플레이 중에는 UI 스킨을 적용할 수 없습니다.");
                return;
            }
            var skin = BuildSkin();

            // InGame 씬이 안 열려 있으면 잠깐 같이 열었다가 저장 후 닫는다 (지금 작업 중인 씬은 그대로)
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = false;
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                opened = true;
            }
            UIManager ui = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                ui = root.GetComponentInChildren<UIManager>(true);
                if (ui) break;
            }
            if (!ui)
            {
                Debug.LogError("InGame 씬에서 UIManager 를 찾지 못했습니다.");
                if (opened) EditorSceneManager.CloseScene(scene, true);
                return;
            }
            Apply(ui, skin);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (opened) EditorSceneManager.CloseScene(scene, true);
            Debug.Log("UI 스킨 적용 완료 (InGame)");
        }

        // ───────── 스킨 에셋 ─────────

        static Sprite Spr(string name, Vector4 border)
        {
            string path = ArtDir + name + ".png";
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!ti) return null;
            bool dirty = ti.textureType != TextureImporterType.Sprite || ti.spriteBorder != border || ti.mipmapEnabled;
            if (dirty)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.spriteBorder = border;
                ti.maxTextureSize = 1024;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public static UiSkin BuildSkin()
        {
            var skin = AssetDatabase.LoadAssetAtPath<UiSkin>(SkinPath);
            if (!skin)
            {
                skin = ScriptableObject.CreateInstance<UiSkin>();
                AssetDatabase.CreateAsset(skin, SkinPath);
            }
            // 테두리 (왼, 아래, 오른, 위) — 늘어나도 모서리가 안 찌그러지게
            skin.panel = Spr("UI_Panel", new Vector4(70, 70, 70, 70));
            skin.card = Spr("UI_Card", new Vector4(50, 80, 50, 80));
            skin.banner = Spr("UI_Banner", Vector4.zero);
            skin.buttonGold = Spr("UI_ButtonGold", new Vector4(100, 0, 100, 0));
            skin.buttonNavy = Spr("UI_ButtonNavy", new Vector4(96, 0, 96, 0));
            skin.pill = Spr("UI_Pill", new Vector4(86, 0, 86, 0));
            skin.barFrame = Spr("UI_BarFrame", new Vector4(72, 0, 72, 0));
            skin.barFill = Spr("UI_BarFill", new Vector4(50, 0, 50, 0));
            skin.levelBadge = Spr("UI_LevelBadge", Vector4.zero);
            skin.coin = Spr("UI_Coin", Vector4.zero);
            skin.pauseIcon = Spr("UI_PauseIcon", Vector4.zero);
            skin.iconRing = Spr("UI_IconRing", Vector4.zero);
            skin.nodeDone = Spr("UI_NodeDone", Vector4.zero);
            skin.nodeCurrent = Spr("UI_NodeCurrent", Vector4.zero);
            skin.nodeBoss = Spr("UI_NodeBoss", Vector4.zero);
            skin.textDark = new Color(0.28f, 0.1f, 0f);
            skin.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Game/Art/Fonts/Jua-Regular.ttf");
            var ids = (SkillId[])System.Enum.GetValues(typeof(SkillId));
            skin.skillIcons = new Sprite[ids.Length];
            foreach (var id in ids) skin.skillIcons[(int)id] = Spr("SkillIcons/Icon_" + id, Vector4.zero);
            EditorUtility.SetDirty(skin);
            AssetDatabase.SaveAssets();
            return skin;
        }

        // ───────── 적용 ─────────

        static readonly Vector2 Center = new(0.5f, 0.5f);
        static readonly Vector2 Top = new(0.5f, 1f);

        public static void Apply(UIManager ui, UiSkin skin)
        {
            Undo.RegisterFullObjectHierarchyUndo(ui.gameObject, "UI 스킨 적용");
            ui.skin = skin;
            var white = Color.white;

            // 글꼴: 꺼진 창/템플릿(데미지 숫자, 체력바)까지 전부
            if (skin.font)
                foreach (var t in ui.GetComponentsInChildren<Text>(true))
                {
                    t.font = skin.font;
                    if (t.fontStyle == FontStyle.Bold) t.fontStyle = FontStyle.Normal; // Jua 는 원래 굵음 (가짜 굵게는 뭉개짐)
                }

            // ── 상단 HUD ──
            var expBg = ui.expFill.parent.parent.GetComponent<Image>();
            var hud = expBg.transform.parent;

            var badge = Deco(hud, "LevelBadge", skin.levelBadge, new Vector2(0f, 1f), new Vector2(100f, -105f), new Vector2(160f, 166f));
            badge.transform.SetAsLastSibling();
            ui.levelText.transform.SetParent(badge.transform, false);
            Fill(ui.levelText.rectTransform);
            ui.levelText.rectTransform.offsetMin = new Vector2(0f, 14f);
            ui.levelText.fontSize = 36;
            ui.levelText.color = skin.gold;

            var ert = expBg.rectTransform;
            ert.anchorMin = new Vector2(0f, 1f);
            ert.anchorMax = new Vector2(1f, 1f);
            ert.pivot = new Vector2(0.5f, 0.5f);
            ert.offsetMin = new Vector2(165f, -137f);
            ert.offsetMax = new Vector2(-390f, -73f);
            Slice(expBg, skin.barFrame, white);
            var area = (RectTransform)ui.expFill.parent;
            area.offsetMin = new Vector2(12f, 11f);
            area.offsetMax = new Vector2(-12f, -11f);
            Slice(ui.expFill.GetComponent<Image>(), skin.barFill, skin.expColor);

            var coinPill = Deco(hud, "CoinPill", skin.pill, new Vector2(1f, 1f), new Vector2(-255f, -105f), new Vector2(240f, 78f), true);
            Behind(coinPill.transform, ui.coinText.transform);
            var coinIcon = hud.Find("CoinIcon") ? hud.Find("CoinIcon").GetComponent<Image>() : null;
            if (coinIcon)
            {
                coinIcon.sprite = skin.coin;
                coinIcon.color = white;
                coinIcon.preserveAspect = true;
                coinIcon.type = Image.Type.Simple;
                Place(coinIcon.rectTransform, new Vector2(1f, 1f), new Vector2(-355f, -105f), new Vector2(84f, 84f));
                coinIcon.transform.SetAsLastSibling();
            }
            Place(ui.coinText.rectTransform, new Vector2(1f, 1f), new Vector2(-230f, -105f), new Vector2(160f, 64f));
            ui.coinText.alignment = TextAnchor.MiddleCenter;
            ui.coinText.color = white;
            ui.coinText.fontSize = 40;
            ui.coinText.transform.SetAsLastSibling();

            var pause = (RectTransform)ui.pauseButton.transform;
            Place(pause, new Vector2(1f, 1f), new Vector2(-75f, -105f), new Vector2(112f, 112f));
            var pImg = ui.pauseButton.GetComponent<Image>();
            pImg.sprite = skin.pauseIcon;
            pImg.type = Image.Type.Simple;
            pImg.preserveAspect = true;
            pImg.color = white;
            var pLabel = pause.Find("Label");
            if (pLabel) pLabel.gameObject.SetActive(false);

            var roomPill = Deco(hud, "RoomPill", skin.pill, Top, new Vector2(0f, -205f), new Vector2(440f, 72f), true);
            Behind(roomPill.transform, ui.roomText.transform);
            Place(ui.roomText.rectTransform, Top, new Vector2(0f, -205f), new Vector2(440f, 64f));
            ui.roomText.fontSize = 34;
            ui.roomText.color = white;

            var dots = hud.Find("StageDots") as RectTransform;
            if (!dots)
            {
                dots = (RectTransform)new GameObject("StageDots", typeof(RectTransform)).transform;
                dots.gameObject.layer = 5;
                dots.SetParent(hud, false);
            }
            Place(dots, Top, new Vector2(0f, -275f), new Vector2(600f, 46f));
            ui.stageDots = dots;

            var bossBg = ui.bossBar.GetComponent<Image>();
            Place(bossBg.rectTransform, Top, new Vector2(0f, -370f), new Vector2(860f, 60f));
            Slice(bossBg, skin.barFrame, white);
            var bArea = (RectTransform)ui.bossFill.parent;
            bArea.offsetMin = new Vector2(12f, 10f);
            bArea.offsetMax = new Vector2(-12f, -10f);
            Slice(ui.bossFill.GetComponent<Image>(), skin.barFill, skin.bossColor);
            ui.bossName.color = skin.gold;

            ui.toastText.color = skin.gold;

            // ── 스킬 선택 ──
            var skillContent = ui.skillPanel.transform.Find("Content");
            Dim(ui.skillPanel);
            TitleBanner(skillContent, ui.skillTitle, skin, 470f);
            var sub = skillContent.Find("Sub");
            if (sub) sub.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 300f);
            foreach (var card in ui.skillCards)
            {
                var bg = card.GetComponent<Image>();
                bg.rectTransform.sizeDelta = new Vector2(330f, 600f);
                Slice(bg, skin.card, white, 1.15f);
                var tint = card.transform.Find("Tint") ? card.transform.Find("Tint").GetComponent<Image>() : null;
                if (!tint)
                {
                    tint = new GameObject("Tint", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    tint.gameObject.layer = 5;
                    tint.transform.SetParent(card.transform, false);
                }
                tint.transform.SetAsFirstSibling();
                tint.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                tint.type = Image.Type.Sliced;
                tint.raycastTarget = false;
                Fill(tint.rectTransform);
                tint.rectTransform.offsetMin = new Vector2(20f, 22f);
                tint.rectTransform.offsetMax = new Vector2(-20f, -22f);
                card.tint = tint;

                Place(card.icon.rectTransform, Top, new Vector2(0f, -140f), new Vector2(200f, 200f));
                card.nameText.color = skin.gold;
                card.nameText.fontSize = 40;
                Place(card.nameText.rectTransform, Top, new Vector2(0f, -285f), new Vector2(300f, 60f));
                Place(card.descText.rectTransform, Top, new Vector2(0f, -410f), new Vector2(280f, 170f));
                card.levelText.color = skin.gold;
                card.levelText.rectTransform.anchoredPosition = new Vector2(0f, 55f);
            }

            // ── 창들 ──
            Window(ui.angelPanel, skin, new Vector2(0f, 120f), new Vector2(920f, 1000f));
            Window(ui.gameOverPanel, skin, new Vector2(0f, 60f), new Vector2(900f, 820f));
            Window(ui.clearPanel, skin, new Vector2(0f, 90f), new Vector2(920f, 1080f));
            Window(ui.pausePanel, skin, new Vector2(0f, 60f), new Vector2(900f, 1200f));
            {
                var title = ui.pausePanel.transform.Find("Content/Title") as RectTransform;
                if (title) title.anchoredPosition = new Vector2(0f, 560f);
                Place(ui.pauseSkills.rectTransform, Center, new Vector2(0f, 160f), new Vector2(780f, 460f));
                ui.pauseSkills.color = new Color(0.92f, 0.94f, 1f);
                Place((RectTransform)ui.resumeButton.transform, Center, new Vector2(0f, -250f), new Vector2(640f, 150f));
                Place((RectTransform)ui.pauseRetryButton.transform, Center, new Vector2(0f, -420f), new Vector2(640f, 150f));
            }
            {
                var halo = ui.angelPanel.transform.Find("Content/Halo") as RectTransform;
                if (halo) halo.anchoredPosition = new Vector2(0f, 500f);
            }
            foreach (var p in new[] { ui.angelPanel, ui.gameOverPanel, ui.clearPanel, ui.pausePanel })
            {
                var title = p.transform.Find("Content/Title");
                if (title) TitleBanner(p.transform.Find("Content"), title.GetComponent<Text>(), skin, title.GetComponent<RectTransform>().anchoredPosition.y);
            }

            Button(ui.reviveButton, skin, true);
            Button(ui.resumeButton, skin, true);
            Button(ui.clearRetryButton, skin, true);
            Button(ui.angelHealButton, skin, true);
            Button(ui.gameOverRetryButton, skin, false);
            Button(ui.pauseRetryButton, skin, false);
            Button(ui.angelSkillButton, skin, false);

            BuildLobby(ui, skin);
            BuildResult(ui, skin);
            // 결과 화면이 생겼으니 게임오버의 두 번째 버튼은 "포기"
            var goLabel = ui.gameOverRetryButton.transform.Find("Label");
            if (goLabel) goLabel.GetComponent<Text>().text = "포기하기";

            EditorUtility.SetDirty(ui);
        }

        // ───────── 로비 / 결과 ─────────

        const string GameTitle = "궁수의 모험";

        static Text NewText(Transform parent, string name, string s, int size, Color c, UiSkin skin, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var t = parent.Find(name) ? parent.Find(name).GetComponent<Text>() : new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            t.gameObject.layer = 5;
            t.transform.SetParent(parent, false);
            t.font = skin.font ? skin.font : t.font;
            t.text = s;
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            if (!t.GetComponent<Outline>())
            {
                var o = t.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.75f);
                o.effectDistance = new Vector2(2f, -2f);
            }
            return t;
        }

        static Button NewButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, bool primary, UiSkin skin)
        {
            var t = parent.Find(name);
            GameObject go;
            if (t) go = t.gameObject;
            else
            {
                go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)) { layer = 5 };
                go.transform.SetParent(parent, false);
            }
            Place((RectTransform)go.transform, Center, pos, size);
            var lab = NewText(go.transform, "Label", label, 52, Color.white, skin);
            Fill(lab.rectTransform);
            var b = go.GetComponent<Button>();
            Button(b, skin, primary);
            ((RectTransform)go.transform).sizeDelta = size;
            return b;
        }

        static GameObject FullPanel(Transform root, string name)
        {
            var t = root.Find(name);
            GameObject go;
            if (t) go = t.gameObject;
            else
            {
                go = new GameObject(name, typeof(RectTransform), typeof(Image)) { layer = 5 };
                go.transform.SetParent(root, false);
                var c = new GameObject("Content", typeof(RectTransform)) { layer = 5 };
                c.transform.SetParent(go.transform, false);
                Fill((RectTransform)c.transform);
            }
            Fill((RectTransform)go.transform);
            go.GetComponent<Image>().raycastTarget = true;
            return go;
        }

        static void BuildLobby(UIManager ui, UiSkin skin)
        {
            var root = ui.canvas.transform;
            var panel = FullPanel(root, "LobbyPanel");
            // 페이드보다 아래, 다른 창들보다 위
            panel.transform.SetSiblingIndex(ui.fade.transform.GetSiblingIndex());
            var bg = panel.GetComponent<Image>();
            bg.sprite = Spr("UI_LobbyBG", Vector4.zero);
            bg.color = Color.white;
            bg.preserveAspect = false;
            bg.type = Image.Type.Simple;
            var content = panel.transform.Find("Content");

            // 제목: 배너 + 큰 글씨
            var banner = Deco(content, "TitleBanner", skin.banner, Center, new Vector2(0f, 640f), new Vector2(960f, 326f));
            var title = NewText(content, "Title", GameTitle, 92, skin.gold, skin);
            Place(title.rectTransform, Center, new Vector2(0f, 646f), new Vector2(700f, 140f));
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 40;
            title.resizeTextMaxSize = 92;
            Behind(banner.transform, title.transform);

            // 골드 알약
            var pill = Deco(content, "GoldPill", skin.pill, new Vector2(1f, 1f), new Vector2(-200f, -90f), new Vector2(300f, 84f), true);
            var coin = Deco(content, "GoldIcon", skin.coin, new Vector2(1f, 1f), new Vector2(-325f, -90f), new Vector2(90f, 90f));
            ui.lobbyGold = NewText(content, "Gold", "0", 44, Color.white, skin);
            Place(ui.lobbyGold.rectTransform, new Vector2(1f, 1f), new Vector2(-180f, -90f), new Vector2(200f, 70f));
            coin.transform.SetAsLastSibling();

            // 기록 + 시작 버튼 (아래쪽)
            var shade = Deco(content, "BottomShade", null, new Vector2(0.5f, 0f), new Vector2(0f, 300f), new Vector2(1400f, 600f));
            shade.color = new Color(0.02f, 0.03f, 0.08f, 0.55f);
            shade.sprite = null;
            shade.transform.SetSiblingIndex(0);
            ui.lobbyRecord = NewText(content, "Record", "", 36, new Color(0.9f, 0.93f, 1f), skin);
            Place(ui.lobbyRecord.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 430f), new Vector2(980f, 60f));
            ui.lobbyStartButton = NewButton(content, "StartButton", "전투 시작", Vector2.zero, new Vector2(680f, 180f), true, skin);
            Place((RectTransform)ui.lobbyStartButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, 280f), new Vector2(680f, 180f));
            ui.lobbyStartButton.transform.Find("Label").GetComponent<Text>().fontSize = 64;

            ui.lobbyPanel = panel;
            panel.SetActive(false);
        }

        static void BuildResult(UIManager ui, UiSkin skin)
        {
            var root = ui.canvas.transform;
            var panel = FullPanel(root, "ResultPanel");
            panel.transform.SetSiblingIndex(ui.fade.transform.GetSiblingIndex());
            panel.GetComponent<Image>().sprite = null;
            Dim(panel);
            var content = panel.transform.Find("Content");
            Window(panel, skin, new Vector2(0f, 40f), new Vector2(920f, 1260f));

            ui.resultTitle = NewText(content, "Title", "모험 종료", 70, skin.gold, skin);
            Place(ui.resultTitle.rectTransform, Center, new Vector2(0f, 600f), new Vector2(620f, 120f));
            TitleBanner(content, ui.resultTitle, skin, 600f);

            var best = NewText(content, "NewBest", "NEW 최고 기록!", 44, new Color(1f, 0.55f, 0.25f), skin);
            Place(best.rectTransform, Center, new Vector2(0f, 420f), new Vector2(700f, 70f));
            ui.resultNewBest = best.gameObject;

            ui.resultStats = NewText(content, "Stats", "", 46, Color.white, skin);
            Place(ui.resultStats.rectTransform, Center, new Vector2(0f, 230f), new Vector2(780f, 300f));
            ui.resultStats.lineSpacing = 1.15f;

            var skillsTitle = NewText(content, "SkillsTitle", "획득한 스킬", 38, skin.gold, skin);
            Place(skillsTitle.rectTransform, Center, new Vector2(0f, 30f), new Vector2(600f, 60f));
            var sk = content.Find("Skills") as RectTransform;
            if (!sk)
            {
                sk = (RectTransform)new GameObject("Skills", typeof(RectTransform)) { layer = 5 }.transform;
                sk.SetParent(content, false);
            }
            Place(sk, Center, new Vector2(0f, -110f), new Vector2(780f, 220f));
            ui.resultSkills = sk;

            ui.resultRetryButton = NewButton(content, "RetryButton", "다시 도전", new Vector2(0f, -340f), new Vector2(640f, 150f), true, skin);
            ui.resultLobbyButton = NewButton(content, "LobbyButton", "로비로", new Vector2(0f, -510f), new Vector2(640f, 150f), false, skin);

            ui.resultPanel = panel;
            panel.SetActive(false);
        }

        // ───────── 도우미 ─────────

        static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = Center;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>9-slice 그림. 가로로만 잘린 그림(알약 모양)은 높이에 맞춰 테두리 두께를 맞춘다</summary>
        static void Slice(Image img, Sprite s, Color c, float ppuMul = 0f)
        {
            if (!s) return;
            img.sprite = s;
            img.type = Image.Type.Sliced;
            img.color = c;
            img.fillCenter = true;
            if (ppuMul <= 0f)
            {
                float h = img.rectTransform.rect.height;
                if (h <= 1f) h = img.rectTransform.sizeDelta.y;
                ppuMul = s.border.y <= 0f && s.border.w <= 0f && h > 1f ? s.rect.height / h : 1f;
            }
            img.pixelsPerUnitMultiplier = ppuMul;
        }

        /// <summary>장식 그림 (이미 있으면 다시 씀)</summary>
        static Image Deco(Transform parent, string name, Sprite s, Vector2 anchor, Vector2 pos, Vector2 size, bool sliced = false)
        {
            var t = parent.Find(name);
            var img = t ? t.GetComponent<Image>() : new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.gameObject.layer = 5;
            img.transform.SetParent(parent, false);
            img.raycastTarget = false;
            Place(img.rectTransform, anchor, pos, size);
            if (sliced) Slice(img, s, Color.white);
            else
            {
                img.sprite = s;
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
                img.color = Color.white;
            }
            return img;
        }

        /// <summary>장식을 글자 바로 뒤에 둔다 (다시 적용해도 순서가 뒤집히지 않게)</summary>
        static void Behind(Transform deco, Transform front)
        {
            deco.SetSiblingIndex(front.GetSiblingIndex());
            if (deco.GetSiblingIndex() > front.GetSiblingIndex()) front.SetSiblingIndex(deco.GetSiblingIndex());
        }

        static void Dim(GameObject panel)
        {
            var img = panel.GetComponent<Image>();
            if (img) img.color = new Color(0.02f, 0.03f, 0.09f, 0.8f);
        }

        static void Window(GameObject panel, UiSkin skin, Vector2 pos, Vector2 size)
        {
            Dim(panel);
            var content = panel.transform.Find("Content");
            var w = Deco(content, "Window", skin.panel, Center, pos, size, true);
            w.pixelsPerUnitMultiplier = 1f;
            w.transform.SetAsFirstSibling();
        }

        static void TitleBanner(Transform content, Text title, UiSkin skin, float y)
        {
            var b = Deco(content, "Banner", skin.banner, Center, new Vector2(0f, y), new Vector2(860f, 292f));
            Behind(b.transform, title.transform);
            var rt = title.rectTransform;
            rt.anchoredPosition = new Vector2(0f, y + 4f);
            rt.sizeDelta = new Vector2(620f, 120f);
            title.fontSize = Mathf.Min(title.fontSize, 70);
            title.color = skin.gold;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 30;
            title.resizeTextMaxSize = 70;
        }

        static void Button(Button b, UiSkin skin, bool primary)
        {
            if (!b) return;
            var img = b.GetComponent<Image>();
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(Mathf.Max(rt.sizeDelta.x, 640f), 150f);
            Slice(img, primary ? skin.buttonGold : skin.buttonNavy, Color.white);
            var label = b.transform.Find("Label");
            if (!label) return;
            var t = label.GetComponent<Text>();
            t.color = primary ? skin.textDark : Color.white;
            t.fontSize = 52;
            var o = label.GetComponent<Outline>();
            if (o) o.enabled = !primary;
        }
    }
}

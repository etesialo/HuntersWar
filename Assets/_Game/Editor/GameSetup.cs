using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Archer.EditorTools
{
    /// <summary>
    /// 메뉴 "Archer/1. 전체 세팅" 한 번으로 머티리얼, 프리팹, GameConfig, 게임 씬, UI를 모두 만든다.
    /// 다시 실행하면 같은 경로의 에셋을 덮어쓴다.
    /// </summary>
    public static class GameSetup
    {
        const string Root = "Assets/_Game";
        const string PrefabDir = Root + "/Prefabs";
        const string MatDir = Root + "/Materials";
        const string DataDir = Root + "/Data";
        const string SceneDir = Root + "/Scenes";
        const string ScenePath = SceneDir + "/InGame.unity";
        const string ConfigPath = DataDir + "/GameConfig.asset";
        const string FloorTexPath = Root + "/Textures/Floor/Floor_Stone_02.png";
        const string FloorRoomTexPath = Root + "/Textures/Floor/Floor_DemonCastle_Room.png";
        const string FloorRoomNormalPath = Root + "/Textures/Floor/Floor_DemonCastle_Room_N.png";

        static Font font;
        static Sprite spriteRound, spriteCircle;
        static readonly Vector2 Center = new(0.5f, 0.5f);

        [MenuItem("Archer/1. 전체 세팅 (머티리얼 + 프리팹 + 씬)", false, 0)]
        public static void SetupAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                EditorUtility.DisplayProgressBar("Archer", "폴더 준비", 0.1f);
                EnsureFolder(PrefabDir);
                EnsureFolder(MatDir);
                EnsureFolder(DataDir);
                EnsureFolder(SceneDir);

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                EditorUtility.DisplayProgressBar("Archer", "머티리얼 / 프리팹 생성", 0.3f);
                var cfg = BuildAssets();
                CharacterModels.ApplyAll(); // 도형 모델 → WAF 캐릭터 모델
                RoomPrefabs.Generate(cfg); // 이미 있는 방 프리팹은 건드리지 않음

                EditorUtility.DisplayProgressBar("Archer", "씬 / UI 생성", 0.7f);
                BuildScene(cfg);
                EditorSceneManager.SaveScene(scene, ScenePath);

                ConfigureProject();
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            EditorUtility.DisplayDialog("Archer",
                "세팅 완료!\n\nGame 뷰 해상도를 1080x1920(9:16)으로 바꾸고 Play를 누르세요.\n" +
                "조작: 마우스 드래그(가상 조이스틱) 또는 WASD / 방향키", "확인");
        }

        [MenuItem("Archer/2. 게임 씬 열기", false, 1)]
        public static void OpenGameScene()
        {
            if (!File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("Archer", "먼저 '1. 전체 세팅'을 실행하세요.", "확인");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void ConfigureProject()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;

            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ═══════════════ 에셋 ═══════════════

        static Material Mat(string name, Color c, bool unlit = false, float smooth = 0.15f)
        {
            string path = $"{MatDir}/{name}.mat";
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else
            {
                m.shader = shader;
            }
            m.SetColor("_BaseColor", c);
            if (!unlit) m.SetFloat("_Smoothness", smooth);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        const string WafEnv = "Assets/_Game/Art/WAF/01.WorkData/Environment/";
        const string ThemePath = DataDir + "/Theme_DemonCastle.asset";

        /// <summary>
        /// 마왕성 테마: 어두운 보라 톤 바닥·벽, 용암 웅덩이, WAF 프롭으로 장애물/문 기둥/배경.
        /// 세팅 전체를 다시 돌리지 않고 이것만 실행해도 된다.
        /// </summary>
        public static void ApplyDemonCastleTheme(GameConfig cfg)
        {
            var theme = AssetDatabase.LoadAssetAtPath<RoomTheme>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<RoomTheme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }
            theme.obstacles = WafProps(
                "Block/block_purple_03_01", "Block/block_red_05_01", "Block/block_red_06_01",
                "Object/dungeon/obj_oblivion_statue_01", "Object/dungeon/obj_oblivion_statue_02",
                "Object/prologue/obj_ruin_01", "Object/prologue/obj_ruin_02",
                "Object/prologue/obj_crystal_pink_01", "Object/prologue/obj_crystal_pink_02",
                "Object/dungeon/obj_cage_01");
            theme.doorPillar = WafProps("Object/prologue/obj_demon_holder_01").FirstOrDefault();
            theme.backdrop = WafProps(
                "Object/prologue/obj_strange_tree_simple_01", "Object/prologue/obj_mineral_pink_pile_01",
                "Object/prologue/obj_strange_vine_pile_01", "Object/prologue/obj_strange_tree_simple_02");
            // 상단 하늘색 글로우: 원본 머티리얼은 두고 복사본의 틴트만 바꾼다
            theme.topGlow = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Art/WAF/02.ProductData/Environment/02_stage/ch01_stage/glow 1.prefab");
            const string glowMatPath = MatDir + "/Glow_SkyBlue.mat";
            var glowMat = AssetDatabase.LoadAssetAtPath<Material>(glowMatPath);
            if (glowMat == null)
            {
                var src = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Game/Art/WAF/01.WorkData/Environment/Block/Texture/glow_field_02.mat");
                if (src)
                {
                    glowMat = new Material(src);
                    glowMat.SetColor("_TintColor", new Color(0.35f, 0.8f, 1f, 0.32f));
                    AssetDatabase.CreateAsset(glowMat, glowMatPath);
                }
            }
            theme.topGlowMaterial = glowMat;
            EditorUtility.SetDirty(theme);
            cfg.theme = theme;

            // 한 장짜리 바닥은 텍스처 색 그대로, 타일 바닥만 보라 톤으로 물들인다
            cfg.floor = FloorMat();
            var tiled = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Floor_Tiled.mat");
            if (tiled) { tiled.SetColor("_BaseColor", new Color(0.5f, 0.44f, 0.58f)); EditorUtility.SetDirty(tiled); }
            cfg.wall.SetColor("_BaseColor", new Color(0.24f, 0.2f, 0.3f));
            cfg.door.SetColor("_BaseColor", new Color(0.35f, 0.08f, 0.12f));
            // 물 → 용암 (이동만 막는 판정은 그대로)
            cfg.water.SetColor("_BaseColor", new Color(0.7f, 0.15f, 0.03f));
            cfg.water.EnableKeyword("_EMISSION");
            cfg.water.SetColor("_EmissionColor", new Color(0.9f, 0.22f, 0.03f));
            cfg.water.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            foreach (var m in new[] { cfg.floor, cfg.wall, cfg.door, cfg.water }) EditorUtility.SetDirty(m);
            EditorUtility.SetDirty(cfg);
        }

        static GameObject[] WafProps(params string[] names)
        {
            var list = names.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>(WafEnv + n + ".prefab")).ToArray();
            for (int i = 0; i < names.Length; i++)
                if (list[i] == null) Debug.LogWarning($"[Archer] 테마 프롭 없음: {names[i]}");
            return list.Where(p => p != null).ToArray();
        }

        /// <summary>방 한 장짜리 바닥 텍스처가 있으면 그걸, 없으면 타일 바닥을 쓴다.</summary>
        static Material FloorMat()
        {
            // 사용자가 만든 바닥 머티리얼(Room_plan)이 있으면 그걸 쓴다
            var userMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Mesh/Room_plan.mat");
            if (userMat) { FloorTiledMat(); return userMat; }
            FloorTiledMat(); // 타일 바닥도 계속 만들어 둔다 (예비)
            return File.Exists(FloorRoomTexPath) ? FloorRoomMat() : FloorTiledMat();
        }

        /// <summary>
        /// 방 전체(벽 밑 1칸 여유 포함 13x21)에 한 장을 그대로 입힌다. 노멀맵이 있으면 같이 쓴다.
        /// </summary>
        static Material FloorRoomMat()
        {
            var m = Mat("Floor_Room", Color.white, false, 0.05f);
            m.SetFloat("_BumpScale", 0.6f); // 캐주얼 톤: 노멀은 아주 약하게
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(FloorRoomTexPath));
            m.SetTextureScale("_BaseMap", Vector2.one);
            m.SetTextureOffset("_BaseMap", Vector2.zero);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(FloorRoomNormalPath);
            m.SetTexture("_BumpMap", normal);
            if (normal) m.EnableKeyword("_NORMALMAP");
            else m.DisableKeyword("_NORMALMAP");
            return m;
        }

        /// <summary>
        /// 타일 바닥 텍스처는 한 장에 돌판 8x8. 돌판 1개 = 1칸이 되도록 타일링하고,
        /// 줄눈이 칸 경계(반정수 좌표)에 오도록 오프셋을 준다. 세로 줄눈은 이미지 아래에서 63px 위치.
        /// </summary>
        static Material FloorTiledMat()
        {
            const float stonesPerTex = 8f;
            var m = Mat("Floor_Tiled", Color.white, false, 0.1f);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(FloorTexPath));
            m.SetTextureScale("_BaseMap", new Vector2(RoomBuilder.FloorW / stonesPerTex, RoomBuilder.FloorL / stonesPerTex));
            m.SetTextureOffset("_BaseMap", new Vector2(0f, 63f / 1024f));
            return m;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale,
            Material mat, Vector3? euler = null, bool shadows = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (euler.HasValue) go.transform.localEulerAngles = euler.Value;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            if (!shadows) r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        static Transform NewModel(GameObject root)
        {
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            return model;
        }

        static T SavePrefab<T>(GameObject go) where T : Component
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{go.name}.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<T>();
        }

        static CharacterController AddCC(GameObject go, float radius, float height)
        {
            var cc = go.AddComponent<CharacterController>();
            cc.radius = radius;
            cc.height = height;
            cc.center = new Vector3(0f, height * 0.5f, 0f);
            cc.stepOffset = 0.05f;
            cc.skinWidth = 0.03f;
            return cc;
        }

        static GameConfig BuildAssets()
        {
            // 색상
            var playerBody = Mat("PlayerBody", new Color(0.25f, 0.55f, 1f));
            var skin = Mat("Skin", new Color(1f, 0.85f, 0.7f));
            var wood = Mat("Wood", new Color(0.55f, 0.35f, 0.15f));
            var black = Mat("Black", new Color(0.08f, 0.08f, 0.1f));
            var ivory = Mat("Ivory", new Color(0.95f, 0.93f, 0.82f));
            var slimeMat = Mat("Slime", new Color(0.35f, 0.85f, 0.35f), false, 0.6f);
            var kingMat = Mat("KingSlime", new Color(0.15f, 0.6f, 0.25f), false, 0.6f);
            var batMat = Mat("Bat", new Color(0.55f, 0.3f, 0.75f));
            var batWing = Mat("BatWing", new Color(0.35f, 0.2f, 0.5f));
            var archerMat = Mat("SkeletonArcher", new Color(0.85f, 0.3f, 0.3f));
            var chargerMat = Mat("Boar", new Color(1f, 0.55f, 0.15f));
            var plantStem = Mat("PlantStem", new Color(0.2f, 0.45f, 0.25f));
            var plantHead = Mat("PlantHead", new Color(0.7f, 0.25f, 0.6f));
            var bossMat = Mat("Boss", new Color(0.45f, 0.1f, 0.15f));
            var bossDark = Mat("BossDark", new Color(0.15f, 0.12f, 0.15f));
            var glowEye = Mat("GlowEye", new Color(1f, 0.9f, 0.2f), true);
            var angelMat = Mat("Angel", new Color(1f, 0.98f, 0.92f), false, 0.4f);
            var gold = Mat("Gold", new Color(1f, 0.8f, 0.2f), false, 0.7f);
            var goldGlow = Mat("GoldGlow", new Color(1f, 0.85f, 0.3f), true);
            var arrowMat = Mat("Arrow", new Color(1f, 0.95f, 0.6f), true);
            var bulletMat = Mat("EnemyBullet", new Color(1f, 0.3f, 0.6f), true);
            var expMat = Mat("ExpGem", new Color(0.3f, 1f, 0.5f), true);

            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<GameConfig>();
                AssetDatabase.CreateAsset(cfg, ConfigPath);
            }

            cfg.floor = FloorMat();
            cfg.floorModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Art/Mesh/Room_plan.fbx");
            cfg.wallModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Art/Mesh/Roomwall_01.fbx");
            cfg.wall = Mat("Wall", new Color(0.38f, 0.36f, 0.42f));
            cfg.obstacle = Mat("Rock", new Color(0.5f, 0.45f, 0.4f));
            cfg.water = Mat("Water", new Color(0.25f, 0.55f, 0.9f), false, 0.9f);
            cfg.door = Mat("Door", new Color(0.45f, 0.3f, 0.18f));
            cfg.portal = Mat("Portal", new Color(0.4f, 1f, 0.6f), true);
            cfg.telegraph = Mat("Telegraph", new Color(1f, 0.2f, 0.2f), true);
            cfg.orb = Mat("FireOrb", new Color(1f, 0.55f, 0.1f), true);

            // ── 플레이어 ──
            {
                var root = new GameObject("Player");
                AddCC(root, 0.4f, 1.2f);
                var m = NewModel(root);
                Prim(PrimitiveType.Capsule, "Body", m, new Vector3(0f, 0.6f, 0f), new Vector3(0.8f, 0.6f, 0.8f), playerBody);
                Prim(PrimitiveType.Sphere, "Head", m, new Vector3(0f, 1.4f, 0f), Vector3.one * 0.55f, skin);
                Prim(PrimitiveType.Cube, "Bow", m, new Vector3(0f, 0.8f, 0.45f), new Vector3(0.8f, 0.1f, 0.1f), wood);
                Prim(PrimitiveType.Cube, "Quiver", m, new Vector3(0.15f, 0.9f, -0.4f), new Vector3(0.2f, 0.5f, 0.2f), wood, new Vector3(0f, 0f, 20f));
                var pc = root.AddComponent<PlayerController>();
                pc.model = m;
                cfg.player = SavePrefab<PlayerController>(root);
            }

            // ── 발사체 ──
            {
                var root = new GameObject("Arrow");
                Prim(PrimitiveType.Cube, "Shaft", root.transform, Vector3.zero, new Vector3(0.08f, 0.08f, 0.7f), arrowMat, null, false);
                Prim(PrimitiveType.Cube, "Tip", root.transform, new Vector3(0f, 0f, 0.35f), Vector3.one * 0.16f, arrowMat, new Vector3(0f, 45f, 0f), false);
                var p = root.AddComponent<Projectile>();
                p.radius = 0.18f;
                p.lifetime = 2.5f;
                cfg.arrow = SavePrefab<Projectile>(root);
            }
            {
                var root = new GameObject("EnemyBullet");
                Prim(PrimitiveType.Sphere, "Ball", root.transform, Vector3.zero, Vector3.one * 0.4f, bulletMat, null, false);
                var p = root.AddComponent<Projectile>();
                p.radius = 0.2f;
                p.lifetime = 6f;
                cfg.enemyBullet = SavePrefab<Projectile>(root);
            }

            // ── 드랍 ──
            {
                var root = new GameObject("ExpGem");
                Prim(PrimitiveType.Cube, "Gem", root.transform, Vector3.zero, Vector3.one * 0.25f, expMat, new Vector3(45f, 0f, 45f), false);
                root.AddComponent<Pickup>().type = PickupType.Exp;
                cfg.expGem = SavePrefab<Pickup>(root);
            }
            {
                var root = new GameObject("Coin");
                Prim(PrimitiveType.Cylinder, "Coin", root.transform, Vector3.zero, new Vector3(0.35f, 0.03f, 0.35f), gold, new Vector3(90f, 0f, 0f), false);
                root.AddComponent<Pickup>().type = PickupType.Coin;
                cfg.coin = SavePrefab<Pickup>(root);
            }

            // ── 천사 ──
            {
                var root = new GameObject("Angel");
                var m = NewModel(root);
                Prim(PrimitiveType.Capsule, "Body", m, new Vector3(0f, 1f, 0f), new Vector3(0.8f, 0.7f, 0.8f), angelMat);
                Prim(PrimitiveType.Sphere, "Head", m, new Vector3(0f, 1.95f, 0f), Vector3.one * 0.5f, skin);
                Prim(PrimitiveType.Cube, "WingL", m, new Vector3(-0.6f, 1.3f, -0.25f), new Vector3(0.9f, 0.6f, 0.08f), angelMat, new Vector3(0f, 20f, 20f));
                Prim(PrimitiveType.Cube, "WingR", m, new Vector3(0.6f, 1.3f, -0.25f), new Vector3(0.9f, 0.6f, 0.08f), angelMat, new Vector3(0f, -20f, -20f));
                Prim(PrimitiveType.Cylinder, "Halo", m, new Vector3(0f, 2.4f, 0f), new Vector3(0.6f, 0.02f, 0.6f), goldGlow, null, false);
                root.AddComponent<Angel>();
                cfg.angel = SavePrefab<Angel>(root);
            }

            // ── 적 ──
            cfg.slime = MakeEnemy<EnemyChaser>("Slime", 0.45f, 0.9f, true, m =>
            {
                Prim(PrimitiveType.Sphere, "Body", m, new Vector3(0f, 0.4f, 0f), new Vector3(0.95f, 0.8f, 0.95f), slimeMat);
                Prim(PrimitiveType.Sphere, "EyeL", m, new Vector3(-0.18f, 0.55f, 0.4f), Vector3.one * 0.14f, black);
                Prim(PrimitiveType.Sphere, "EyeR", m, new Vector3(0.18f, 0.55f, 0.4f), Vector3.one * 0.14f, black);
            }, e =>
            {
                e.displayName = "슬라임";
                e.maxHp = 60f; e.moveSpeed = 2.2f; e.contactDamage = 50f;
                e.expValue = 2; e.coinValue = 1; e.hpBarOffset = 1.2f; e.hpBarWidth = 80f;
            });

            cfg.bat = MakeEnemy<EnemyBat>("Bat", 0.4f, 0f, false, m =>
            {
                Prim(PrimitiveType.Sphere, "Body", m, new Vector3(0f, 0.9f, 0f), Vector3.one * 0.5f, batMat);
                Prim(PrimitiveType.Cube, "WingL", m, new Vector3(-0.45f, 0.95f, 0f), new Vector3(0.5f, 0.05f, 0.3f), batWing, new Vector3(0f, 0f, 15f));
                Prim(PrimitiveType.Cube, "WingR", m, new Vector3(0.45f, 0.95f, 0f), new Vector3(0.5f, 0.05f, 0.3f), batWing, new Vector3(0f, 0f, -15f));
                Prim(PrimitiveType.Sphere, "EyeL", m, new Vector3(-0.1f, 0.98f, 0.22f), Vector3.one * 0.08f, glowEye, null, false);
                Prim(PrimitiveType.Sphere, "EyeR", m, new Vector3(0.1f, 0.98f, 0.22f), Vector3.one * 0.08f, glowEye, null, false);
            }, e =>
            {
                var col = e.gameObject.AddComponent<SphereCollider>();
                col.isTrigger = true;
                col.center = new Vector3(0f, 0.7f, 0f);
                col.radius = 0.45f;
                e.displayName = "박쥐";
                e.maxHp = 35f; e.moveSpeed = 3.2f; e.contactDamage = 40f;
                e.expValue = 1; e.coinValue = 1; e.hpBarOffset = 1.5f; e.hpBarWidth = 70f;
            });

            cfg.archer = MakeEnemy<EnemyShooter>("SkeletonArcher", 0.4f, 1.2f, true, m =>
            {
                Prim(PrimitiveType.Capsule, "Body", m, new Vector3(0f, 0.6f, 0f), new Vector3(0.75f, 0.6f, 0.75f), archerMat);
                Prim(PrimitiveType.Sphere, "Head", m, new Vector3(0f, 1.35f, 0f), Vector3.one * 0.5f, ivory);
                Prim(PrimitiveType.Cube, "Bow", m, new Vector3(0f, 0.8f, 0.42f), new Vector3(0.7f, 0.1f, 0.1f), wood);
            }, e =>
            {
                e.displayName = "해골 궁수";
                e.maxHp = 50f; e.moveSpeed = 1.8f; e.contactDamage = 30f; e.attackDamage = 70f;
                e.expValue = 3; e.coinValue = 1; e.hpBarOffset = 1.9f;
            });

            cfg.charger = MakeEnemy<EnemyCharger>("Boar", 0.5f, 1f, true, m =>
            {
                Prim(PrimitiveType.Cube, "Body", m, new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.8f, 1.1f), chargerMat);
                Prim(PrimitiveType.Cube, "HornL", m, new Vector3(-0.25f, 0.55f, 0.65f), new Vector3(0.1f, 0.1f, 0.35f), ivory, new Vector3(-20f, 0f, 0f));
                Prim(PrimitiveType.Cube, "HornR", m, new Vector3(0.25f, 0.55f, 0.65f), new Vector3(0.1f, 0.1f, 0.35f), ivory, new Vector3(-20f, 0f, 0f));
            }, e =>
            {
                e.displayName = "멧돼지";
                e.maxHp = 80f; e.moveSpeed = 1.5f; e.contactDamage = 70f;
                e.expValue = 3; e.coinValue = 2; e.hpBarOffset = 1.4f;
            });

            cfg.turret = MakeEnemy<EnemyTurret>("Plant", 0.45f, 0f, false, m =>
            {
                Prim(PrimitiveType.Cylinder, "Stem", m, new Vector3(0f, 0.3f, 0f), new Vector3(0.9f, 0.3f, 0.9f), plantStem);
                Prim(PrimitiveType.Sphere, "Head", m, new Vector3(0f, 0.95f, 0f), Vector3.one * 0.75f, plantHead);
                Prim(PrimitiveType.Cube, "Mouth", m, new Vector3(0f, 0.95f, 0.35f), new Vector3(0.35f, 0.15f, 0.1f), black);
            }, e =>
            {
                var col = e.gameObject.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, 0.6f, 0f);
                col.radius = 0.45f;
                col.height = 1.2f;
                e.displayName = "식인 꽃";
                e.maxHp = 70f; e.moveSpeed = 0f; e.contactDamage = 30f; e.attackDamage = 60f;
                e.expValue = 3; e.coinValue = 2; e.hpBarOffset = 1.6f;
            });

            cfg.miniBoss = MakeEnemy<EnemyMiniBoss>("KingSlime", 1f, 2f, true, m =>
            {
                Prim(PrimitiveType.Sphere, "Body", m, new Vector3(0f, 0.85f, 0f), new Vector3(2.1f, 1.7f, 2.1f), kingMat);
                Prim(PrimitiveType.Cylinder, "Crown", m, new Vector3(0f, 1.8f, 0f), new Vector3(0.7f, 0.2f, 0.7f), gold);
                Prim(PrimitiveType.Sphere, "EyeL", m, new Vector3(-0.4f, 1.15f, 0.85f), Vector3.one * 0.25f, black);
                Prim(PrimitiveType.Sphere, "EyeR", m, new Vector3(0.4f, 1.15f, 0.85f), Vector3.one * 0.25f, black);
            }, e =>
            {
                e.displayName = "킹 슬라임";
                e.isBoss = true;
                e.maxHp = 700f; e.moveSpeed = 1.4f; e.contactDamage = 80f; e.attackDamage = 70f;
                e.expValue = 15; e.coinValue = 10; e.hpBarOffset = 2.4f;
            });

            cfg.boss = MakeEnemy<EnemyBoss>("DarkKnight", 1.1f, 2.4f, true, m =>
            {
                Prim(PrimitiveType.Cube, "Body", m, new Vector3(0f, 1.1f, 0f), new Vector3(2f, 1.8f, 1.5f), bossMat);
                Prim(PrimitiveType.Cube, "Head", m, new Vector3(0f, 2.3f, 0.1f), new Vector3(1f, 0.8f, 0.9f), bossDark);
                Prim(PrimitiveType.Cube, "HornL", m, new Vector3(-0.55f, 2.8f, 0.1f), new Vector3(0.15f, 0.7f, 0.15f), ivory, new Vector3(0f, 0f, 25f));
                Prim(PrimitiveType.Cube, "HornR", m, new Vector3(0.55f, 2.8f, 0.1f), new Vector3(0.15f, 0.7f, 0.15f), ivory, new Vector3(0f, 0f, -25f));
                Prim(PrimitiveType.Cube, "EyeL", m, new Vector3(-0.22f, 2.35f, 0.56f), new Vector3(0.2f, 0.1f, 0.05f), glowEye, null, false);
                Prim(PrimitiveType.Cube, "EyeR", m, new Vector3(0.22f, 2.35f, 0.56f), new Vector3(0.2f, 0.1f, 0.05f), glowEye, null, false);
                Prim(PrimitiveType.Cube, "ArmL", m, new Vector3(-1.2f, 1.1f, 0.2f), new Vector3(0.45f, 1.4f, 0.45f), bossDark);
                Prim(PrimitiveType.Cube, "ArmR", m, new Vector3(1.2f, 1.1f, 0.2f), new Vector3(0.45f, 1.4f, 0.45f), bossDark);
            }, e =>
            {
                e.displayName = "어둠의 기사";
                e.isBoss = true;
                e.maxHp = 2200f; e.moveSpeed = 1.6f; e.contactDamage = 100f; e.attackDamage = 80f;
                e.expValue = 30; e.coinValue = 30; e.hpBarOffset = 3.4f;
            });

            ApplyDemonCastleTheme(cfg);

            EditorUtility.SetDirty(cfg);
            return cfg;
        }

        static T MakeEnemy<T>(string name, float radius, float height, bool useCC,
            System.Action<Transform> buildModel, System.Action<T> setup) where T : Enemy
        {
            var root = new GameObject(name);
            var model = NewModel(root);
            buildModel(model);
            if (useCC) AddCC(root, radius, height);
            var e = root.AddComponent<T>();
            e.model = model;
            e.bodyRadius = radius;
            setup(e);
            return SavePrefab<T>(root);
        }

        // ═══════════════ 씬 ═══════════════

        static void BuildScene(GameConfig cfg)
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.65f);

            // 9:16 바깥을 검게 칠하는 카메라
            var bgCam = new GameObject("LetterboxCamera").AddComponent<Camera>();
            bgCam.clearFlags = CameraClearFlags.SolidColor;
            bgCam.backgroundColor = Color.black;
            bgCam.cullingMask = 0;
            bgCam.depth = -10;

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.08f, 0.11f);
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 200f;
            cam.depth = 0;
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<CameraFollow>();
            camGo.AddComponent<AspectLock>();
            camGo.transform.SetPositionAndRotation(new Vector3(0f, 26f, -15f), Quaternion.Euler(60f, 0f, 0f));

            var gmGo = new GameObject("GameManager");
            var gm = gmGo.AddComponent<GameManager>();
            var stage = gmGo.AddComponent<StageManager>();

            BuildUI(cam, out var ui, out var joy);

            gm.config = cfg;
            gm.ui = ui;
            gm.stage = stage;
            gm.cam = follow;
            gm.joystick = joy;

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        // ═══════════════ UI ═══════════════

        static void LoadUiResources()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            spriteRound = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            spriteCircle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = Center;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static Image Img(string name, Transform parent, Color c, Sprite sprite = null, bool raycast = false)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.color = c;
            img.sprite = sprite;
            if (sprite == spriteRound && sprite != null) img.type = Image.Type.Sliced;
            img.raycastTarget = raycast;
            return img;
        }

        static Text Txt(string name, Transform parent, string s, int size, Color c,
            TextAnchor align = TextAnchor.MiddleCenter, bool outline = true)
        {
            var t = Rect(name, parent).gameObject.AddComponent<Text>();
            t.font = font;
            t.text = s;
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            if (outline)
            {
                var o = t.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.8f);
                o.effectDistance = new Vector2(2f, -2f);
            }
            return t;
        }

        static Button Btn(string name, Transform parent, string label, Vector2 pos, Vector2 size, Color c, int fontSize = 48)
        {
            var img = Img(name, parent, c, spriteRound, true);
            Place(img.rectTransform, Center, pos, size);
            var b = img.gameObject.AddComponent<Button>();
            Stretch(Txt("Label", img.transform, label, fontSize, Color.white).rectTransform);
            return b;
        }

        /// <summary>어두운 전체 배경 + 팝 애니메이션 대상인 Content</summary>
        static GameObject Panel(string name, Transform parent, out Transform content)
        {
            var img = Img(name, parent, new Color(0f, 0f, 0f, 0.78f), null, true);
            Stretch(img.rectTransform);
            content = Stretch(Rect("Content", img.transform));
            return img.gameObject;
        }

        /// <summary>배경 + 채우기(앵커 비율로 길이 조절) 막대</summary>
        static RectTransform Bar(string name, Transform parent, Color bg, Color fill, out Image bgImage, float inset = 4f)
        {
            bgImage = Img(name, parent, bg, spriteRound);
            var area = Stretch(Rect("FillArea", bgImage.transform));
            area.offsetMin = new Vector2(inset, inset);
            area.offsetMax = new Vector2(-inset, -inset);
            var f = Img("Fill", area, fill).rectTransform;
            f.anchorMin = Vector2.zero;
            f.anchorMax = Vector2.one;
            f.pivot = new Vector2(0f, 0.5f);
            f.offsetMin = f.offsetMax = Vector2.zero;
            return f;
        }

        static void BuildUI(Camera cam, out UIManager ui, out VirtualJoystick joy)
        {
            LoadUiResources();
            var white = Color.white;
            var yellow = new Color(1f, 0.85f, 0.3f);
            var goldColor = new Color(1f, 0.8f, 0.2f);

            var canvasGo = new GameObject("Canvas", typeof(RectTransform)) { layer = 5 };
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            ui = canvasGo.AddComponent<UIManager>();
            ui.canvas = canvas;
            var root = canvasGo.transform;

            // ── 조이스틱 (그림 없음: 화면 어디든 누른 곳이 중심) ──
            var joyGo = new GameObject("Joystick", typeof(RectTransform));
            joyGo.transform.SetParent(root, false);
            joy = joyGo.AddComponent<VirtualJoystick>();

            // ── 월드 UI (체력바, 데미지 숫자) ──
            var world = Stretch(Rect("WorldLayer", root));
            ui.worldLayer = world;
            {
                var fill = Bar("HpBarTemplate", world, new Color(0f, 0f, 0f, 0.75f), Color.red, out var bg, 3f);
                bg.sprite = null;
                Place(bg.rectTransform, Center, Vector2.zero, new Vector2(100f, 16f));
                var label = Txt("Label", bg.transform, "100", 28, white);
                Place(label.rectTransform, Center, new Vector2(0f, 28f), new Vector2(200f, 40f));
                var hb = bg.gameObject.AddComponent<HpBar>();
                hb.fill = fill;
                hb.fillImage = fill.GetComponent<Image>();
                hb.label = label;
                bg.gameObject.SetActive(false);
                ui.hpBarTemplate = hb;

                var pop = Txt("PopupTemplate", world, "0", 46, white);
                Place(pop.rectTransform, Center, Vector2.zero, new Vector2(300f, 80f));
                pop.gameObject.SetActive(false);
                ui.popupTemplate = pop;
            }

            // ── HUD ──
            var hud = Stretch(Rect("HUD", root));
            {
                var expFill = Bar("ExpBar", hud, new Color(0f, 0f, 0f, 0.6f), new Color(0.35f, 0.75f, 1f), out var expBg);
                var ert = expBg.rectTransform;
                ert.anchorMin = new Vector2(0f, 1f);
                ert.anchorMax = new Vector2(1f, 1f);
                ert.pivot = new Vector2(0.5f, 1f);
                ert.anchoredPosition = new Vector2(0f, -40f);
                ert.sizeDelta = new Vector2(-80f, 48f);
                expFill.anchorMax = new Vector2(0f, 1f);
                ui.expFill = expFill;
                ui.levelText = Txt("LevelText", expBg.transform, "Lv.1", 36, white);
                Stretch(ui.levelText.rectTransform);

                ui.roomText = Txt("RoomText", hud, "1 / 10", 46, white);
                Place(ui.roomText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(600f, 64f));

                var coinIcon = Img("CoinIcon", hud, goldColor, spriteCircle);
                Place(coinIcon.rectTransform, new Vector2(1f, 1f), new Vector2(-210f, -130f), new Vector2(48f, 48f));
                ui.coinText = Txt("CoinText", hud, "0", 42, goldColor, TextAnchor.MiddleLeft);
                Place(ui.coinText.rectTransform, new Vector2(1f, 1f), new Vector2(-105f, -130f), new Vector2(140f, 64f));

                ui.pauseButton = Btn("PauseButton", hud, "II", Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.5f), 44);
                Place((RectTransform)ui.pauseButton.transform, new Vector2(0f, 1f), new Vector2(90f, -130f), new Vector2(96f, 96f));

                var bossFill = Bar("BossBar", hud, new Color(0f, 0f, 0f, 0.7f), new Color(0.9f, 0.15f, 0.2f), out var bossBg);
                Place(bossBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -230f), new Vector2(840f, 40f));
                ui.bossBar = bossBg.gameObject;
                ui.bossFill = bossFill;
                ui.bossName = Txt("BossName", bossBg.transform, "보스", 34, white);
                Place(ui.bossName.rectTransform, Center, new Vector2(0f, 44f), new Vector2(800f, 50f));

                ui.toastText = Txt("Toast", hud, "", 76, yellow);
                Place(ui.toastText.rectTransform, Center, new Vector2(0f, 380f), new Vector2(1040f, 130f));
            }

            // ── 스킬 선택 ──
            {
                ui.skillPanel = Panel("SkillPanel", root, out var c);
                ui.skillTitle = Txt("Title", c, "레벨 업!", 84, yellow);
                Place(ui.skillTitle.rectTransform, Center, new Vector2(0f, 480f), new Vector2(1000f, 120f));
                var sub = Txt("Sub", c, "스킬을 하나 선택하세요", 40, white);
                Place(sub.rectTransform, Center, new Vector2(0f, 390f), new Vector2(1000f, 60f));
                ui.skillCards = new SkillCard[3];
                for (int i = 0; i < 3; i++) ui.skillCards[i] = Card(c, new Vector2((i - 1) * 345f, 0f), goldColor);
            }

            // ── 천사 ──
            {
                ui.angelPanel = Panel("AngelPanel", root, out var c);
                var halo = Img("Halo", c, new Color(1f, 0.9f, 0.4f), spriteCircle);
                Place(halo.rectTransform, Center, new Vector2(0f, 440f), new Vector2(200f, 200f));
                var title = Txt("Title", c, "천사의 축복", 80, new Color(1f, 0.95f, 0.6f));
                Place(title.rectTransform, Center, new Vector2(0f, 250f), new Vector2(1000f, 120f));
                var sub = Txt("Sub", c, "하나를 선택하세요", 40, white);
                Place(sub.rectTransform, Center, new Vector2(0f, 160f), new Vector2(1000f, 60f));
                ui.angelHealButton = Btn("HealButton", c, "체력 40% 회복", new Vector2(0f, -20f), new Vector2(760f, 170f), new Color(0.25f, 0.65f, 0.35f), 52);
                ui.angelSkillButton = Btn("SkillButton", c, "스킬 하나 획득", new Vector2(0f, -230f), new Vector2(760f, 170f), new Color(0.4f, 0.35f, 0.8f), 52);
            }

            // ── 게임 오버 ──
            {
                ui.gameOverPanel = Panel("GameOverPanel", root, out var c);
                var title = Txt("Title", c, "쓰러졌습니다...", 90, new Color(1f, 0.4f, 0.4f));
                Place(title.rectTransform, Center, new Vector2(0f, 320f), new Vector2(1000f, 140f));
                ui.reviveButton = Btn("ReviveButton", c, "부활하기 (1회)", new Vector2(0f, 0f), new Vector2(700f, 160f), new Color(0.25f, 0.65f, 0.35f));
                ui.gameOverRetryButton = Btn("RetryButton", c, "처음부터", new Vector2(0f, -210f), new Vector2(700f, 160f), new Color(0.35f, 0.35f, 0.4f));
            }

            // ── 클리어 ──
            {
                ui.clearPanel = Panel("ClearPanel", root, out var c);
                var title = Txt("Title", c, "스테이지 클리어!", 90, goldColor);
                Place(title.rectTransform, Center, new Vector2(0f, 440f), new Vector2(1000f, 140f));
                ui.clearStats = Txt("Stats", c, "", 50, white);
                Place(ui.clearStats.rectTransform, Center, new Vector2(0f, 120f), new Vector2(900f, 400f));
                ui.clearRetryButton = Btn("RetryButton", c, "다시 도전", new Vector2(0f, -260f), new Vector2(700f, 160f), new Color(0.3f, 0.5f, 0.9f));
            }

            // ── 일시정지 ──
            {
                ui.pausePanel = Panel("PausePanel", root, out var c);
                var title = Txt("Title", c, "일시정지", 84, white);
                Place(title.rectTransform, Center, new Vector2(0f, 620f), new Vector2(1000f, 120f));
                ui.pauseSkills = Txt("Skills", c, "", 38, white, TextAnchor.UpperCenter);
                Place(ui.pauseSkills.rectTransform, Center, new Vector2(0f, 260f), new Vector2(900f, 560f));
                ui.resumeButton = Btn("ResumeButton", c, "계속하기", new Vector2(0f, -220f), new Vector2(700f, 160f), new Color(0.25f, 0.65f, 0.35f));
                ui.pauseRetryButton = Btn("RetryButton", c, "처음부터", new Vector2(0f, -420f), new Vector2(700f, 160f), new Color(0.35f, 0.35f, 0.4f));
            }

            // ── 페이드 (가장 위) ──
            // 편집 중엔 투명 (게임 시작 시 UIManager 가 1로 올렸다가 걷어낸다)
            ui.fade = Img("Fade", root, new Color(0f, 0f, 0f, 0f), null, true);
            Stretch(ui.fade.rectTransform);

            // 팝업 패널은 편집 중 Game 뷰를 가리지 않게 꺼둔다 (게임 중엔 UIManager 가 켜고 끈다)
            foreach (var p in new[] { ui.skillPanel, ui.angelPanel, ui.gameOverPanel, ui.clearPanel, ui.pausePanel, ui.bossBar })
                if (p) p.SetActive(false);
            if (ui.toastText) ui.toastText.gameObject.SetActive(false);

            // UI 그림이 있으면 네이비·골드 스킨을 바로 입힌다
            if (File.Exists("Assets/_Game/Art/UI/UI_Panel.png"))
                UiSkinApplier.Apply(ui, UiSkinApplier.BuildSkin());
        }

        static SkillCard Card(Transform parent, Vector2 pos, Color levelColor)
        {
            var bg = Img("Card", parent, new Color(0.16f, 0.18f, 0.26f), spriteRound, true);
            Place(bg.rectTransform, Center, pos, new Vector2(320f, 560f));
            var card = bg.gameObject.AddComponent<SkillCard>();
            card.button = bg.gameObject.AddComponent<Button>();

            card.icon = Img("Icon", bg.transform, Color.white, spriteRound);
            Place(card.icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(180f, 180f));
            card.iconText = Txt("Letter", card.icon.transform, "?", 96, Color.white);
            Stretch(card.iconText.rectTransform);

            card.nameText = Txt("Name", bg.transform, "스킬", 40, new Color(1f, 0.95f, 0.7f));
            Place(card.nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(300f, 60f));
            card.descText = Txt("Desc", bg.transform, "", 30, new Color(0.85f, 0.87f, 0.92f), TextAnchor.UpperCenter, false);
            Place(card.descText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -385f), new Vector2(285f, 170f));
            card.levelText = Txt("Level", bg.transform, "", 34, levelColor);
            Place(card.levelText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 45f), new Vector2(300f, 50f));
            return card;
        }
    }
}

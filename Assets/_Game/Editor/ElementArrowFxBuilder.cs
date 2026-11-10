using UnityEditor;
using UnityEngine;

namespace Archer.EditorTools
{
    /// <summary>
    /// 번개 / 바람 / 바위 화살 연출을 Arrow 프리팹에 만든다. 그림은 Higgsfield 로 만든 것.
    /// - 번개: 화살 대신 파란 스파크 번개 줄기(T_LightningBolt)가 날아가고, 짧은 전기 줄기(ArrowCrackle)가 튄다
    /// - 바람: 화살 대신 곧게 뻗은 바람 줄기(T_WindGust)가 날아가고, 옅은 바람 자국이 남는다
    /// - 바위: 화살 대신 끝이 뭉툭한 돌기둥(T_RockPillar)이 날아가고, 흙먼지 꼬리(T_RockArrowTrail)가 남는다
    /// "만들기" 는 없는 것만, "다시 만들기" 는 셋 다 지우고 새로 만든다.
    /// </summary>
    public static class ElementArrowFxBuilder
    {
        const string ArrowPath = "Assets/_Game/Prefabs/Arrow.prefab";
        const string MatDir = "Assets/_Game/Materials/FX";
        const string FxDir = "Assets/_Game/Prefabs/FX";
        const string TexDir = "Assets/_Game/Art/FX/Textures";

        static readonly Color LightningTint = new(0.7f, 0.85f, 1f);
        static readonly Color WindTint = new(0.7f, 1f, 0.88f);
        static readonly Color RockTint = new(0.72f, 0.58f, 0.42f);

        [MenuItem("Archer/속성 화살 연출 만들기 (번개·바람·바위)", false, 40)]
        public static void Build() => Run(false);

        [MenuItem("Archer/속성 화살 연출 다시 만들기 (번개·바람·바위)", false, 41)]
        public static void Rebuild() => Run(true);

        static void Run(bool rebuild)
        {
            var boltMat = FxSpriteMat("M_LightningBolt", "M_TrailFrost", "T_LightningBolt", new Color(0.9f, 1f, 1.4f, 1f), 2.6f, 0.04f, 30f, 60f);
            var gustMat = FxSpriteMat("M_WindGust", "M_TrailFrost", "T_WindGust", new Color(0.95f, 1.15f, 1.05f, 0.9f), 2.2f, 0.03f, 6f, 20f);
            var clusterMat = AlphaSpriteMat("M_RockCluster", "T_RockCluster");
            var debrisMat = AlphaSpriteMat("M_RockDebris", "T_RockDebris");
            var windTrail = TrailPrefab("FX_WindTrail", new Color(0.85f, 1f, 0.95f, 0.45f), new Color(0.7f, 1f, 0.9f, 0.3f), 0.7f);
            var rockDebris = DebrisPrefab("FX_RockDebris", debrisMat);

            var root = PrefabUtility.LoadPrefabContents(ArrowPath);
            try
            {
                var proj = root.GetComponent<Projectile>();
                if (rebuild)
                    foreach (var n in new[] { "LightningFx", "WindFx", "RockFx" })
                    {
                        var t = root.transform.Find(n);
                        if (t) Object.DestroyImmediate(t.gameObject);
                    }

                proj.lightningFx = Find(root, "LightningFx") ?? MakeLightning(root, boltMat);
                proj.windFx = Find(root, "WindFx") ?? MakeWind(root, gustMat, windTrail);
                proj.rockFx = Find(root, "RockFx") ?? MakeRock(root, clusterMat, rockDebris);

                PrefabUtility.SaveAsPrefabAsset(root, ArrowPath);
                Debug.Log("[Archer] 번개·바람·바위 화살 연출을 Arrow 프리팹에 반영했어요.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        static ArrowTrailFx Find(GameObject root, string name)
        {
            var t = root.transform.Find(name);
            return t ? t.GetComponent<ArrowTrailFx>() : null;
        }

        // ───────── 번개: 화살 몸통 대신 번개 줄기 그림 (둥근 빛 덩어리 없음) ─────────

        static ArrowTrailFx MakeLightning(GameObject root, Material boltMat)
        {
            var go = Clone(root, "FrostFx", "LightningFx");
            Remove(go, "Head");
            Object.DestroyImmediate(go.GetComponent<TrailEmitter>()); // 연기·안개 없음

            var fx = go.GetComponent<ArrowTrailFx>();
            fx.tint = LightningTint;
            fx.hideBody = true;
            fx.growTime = 0.06f;
            // 번개 줄기 = 꼬리 쿼드 (텍스처 1024 x 256 → 4:1). 촉 끝이 화살 머리 근처
            var streak = go.transform.Find("Streak");
            streak.GetComponent<Renderer>().sharedMaterial = boltMat;
            streak.localScale = new Vector3(2.6f, 0.65f, 1f);
            streak.localPosition = new Vector3(0f, 0.02f, -0.85f);
            fx.streak = streak;

            // 방울 입자는 얼음 화살과 헷갈려서 빼고, 지나온 길에 지지직거리는 직선 전기 궤적
            Remove(go, "Sparkles");
            go.AddComponent<ElectricTrail>();
            var c = go.AddComponent<ArrowCrackle>();
            c.color = new Color(0.65f, 0.85f, 1f, 1f);
            c.count = 3;
            return fx;
        }

        // ───────── 바람: 화살 몸통 대신 곧게 뻗은 바람 줄기 ─────────

        static ArrowTrailFx MakeWind(GameObject root, Material gustMat, ParticleSystem trail)
        {
            var go = Clone(root, "FrostFx", "WindFx");
            Remove(go, "Head");

            var fx = go.GetComponent<ArrowTrailFx>();
            fx.tint = WindTint;
            fx.hideBody = true;
            fx.growTime = 0.1f;
            // 텍스처 1024 x 159 (약 6.4:1). 조금 두툼하게
            var streak = go.transform.Find("Streak");
            streak.GetComponent<Renderer>().sharedMaterial = gustMat;
            streak.localScale = new Vector3(3.0f, 0.6f, 1f);
            streak.localPosition = new Vector3(0f, 0.02f, -1.0f);
            fx.streak = streak;

            Recolor(go.transform.Find("Sparkles"), new Color(0.9f, 1f, 0.95f, 0.8f), new Color(0.6f, 1f, 0.85f, 0.6f));
            var te = go.GetComponent<TrailEmitter>();
            if (te && trail) te.prefab = trail;
            return fx;
        }

        // ───────── 바위: 화살 몸통 대신 돌 블록이 뭉친 길쭉한 바위 + 날아가며 돌·흙 조각이 떨어짐 ─────────

        static ArrowTrailFx MakeRock(GameObject root, Material clusterMat, ParticleSystem debris)
        {
            var go = Clone(root, "FrostFx", "RockFx");
            var fx = go.GetComponent<ArrowTrailFx>();
            fx.tint = RockTint;
            fx.hideBody = true;
            fx.growTime = 0.12f;

            // 연기 꼬리·불티 입자는 없음 (돌과 흙이 떨어지는 것만)
            Remove(go, "Streak");
            Remove(go, "Sparkles");
            fx.streak = null;

            var head = go.transform.Find("Head");
            if (head)
            {
                var hr = head.GetComponent<Renderer>();
                hr.sharedMaterial = clusterMat;
                hr.sortingOrder = 5;
                // 텍스처 1024 x 344 (약 3:1) → 길이 1.5m, 굵기 0.55m (빼빼로처럼 가늘지 않게)
                head.localScale = new Vector3(1.5f, 0.55f, 1f);
                head.localPosition = new Vector3(0f, 0.05f, -0.25f);
            }

            var te = go.GetComponent<TrailEmitter>();
            if (te)
            {
                te.prefab = debris;
                te.perMeter = 3.5f; // 1m 날아갈 때마다 3~4개
                te.perSecond = 0f;
            }
            return fx;
        }

        /// <summary>
        /// 돌·흙 조각: 2x2 그림 중 하나를 골라 튀어나와서 중력으로 떨어지고, 바닥(y=0)에 튕긴 뒤 사라짐.
        /// TrailEmitter 가 씬에 하나만 만들어 공용으로 쓴다 (원점에 생기므로 자식 판이 곧 바닥 높이).
        /// </summary>
        static ParticleSystem DebrisPrefab(string name, Material mat)
        {
            var path = $"{FxDir}/{name}.prefab";
            var go = new GameObject(name);
            try
            {
                var ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.loop = true;
                main.playOnAwake = true;
                main.duration = 1f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.1f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.36f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.gravityModifier = 2.2f;
                main.maxParticles = 300;

                var em = ps.emission;
                em.rateOverTime = 0f; // TrailEmitter 가 직접 뿜음

                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Hemisphere;
                shape.radius = 0.15f;
                shape.rotation = new Vector3(-90f, 0f, 0f); // 위쪽 반구로 살짝 튀어오름

                var rot = ps.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

                var sheet = ps.textureSheetAnimation;
                sheet.enabled = true;
                sheet.numTilesX = 2;
                sheet.numTilesY = 2;
                sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
                sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 3.999f);
                sheet.cycleCount = 1;

                // 끝에 가서 서서히 투명하게
                var col = ps.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
                col.color = g;

                // 바닥에 닿으면 툭 튕기고 굴러가다 멈춤
                var floor = new GameObject("Floor").transform;
                floor.SetParent(go.transform, false);
                var collision = ps.collision;
                collision.enabled = true;
                collision.type = ParticleSystemCollisionType.Planes;
                collision.SetPlane(0, floor);
                collision.bounce = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
                collision.dampen = new ParticleSystem.MinMaxCurve(0.5f, 0.7f);
                collision.radiusScale = 0.5f;

                var r = go.GetComponent<ParticleSystemRenderer>();
                r.sharedMaterial = mat;
                r.renderMode = ParticleSystemRenderMode.Billboard;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.sortingOrder = 4;

                PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally { Object.DestroyImmediate(go); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<ParticleSystem>();
        }

        // ───────── 재료 ─────────

        static GameObject Clone(GameObject root, string src, string name)
        {
            var go = Object.Instantiate(root.transform.Find(src).gameObject, root.transform);
            go.name = name;
            go.SetActive(false);
            return go;
        }

        static void Remove(GameObject go, string child)
        {
            var t = go.transform.Find(child);
            if (t) Object.DestroyImmediate(t.gameObject);
        }

        /// <summary>FxSprite(밝기 = 투명도) 재질. 값은 매번 다시 넣는다 (숫자를 바꾸고 "다시 만들기" 하면 반영)</summary>
        static Material FxSpriteMat(string name, string from, string tex, Color tint, float alphaBoost, float wobble, float freq, float speed)
        {
            var path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                AssetDatabase.CopyAsset($"{MatDir}/{from}.mat", path);
                m = AssetDatabase.LoadAssetAtPath<Material>(path);
            }
            if (tex != null) m.SetTexture("_MainTex", Tex(tex, false));
            m.SetColor("_TintColor", tint);
            m.SetFloat("_AlphaBoost", alphaBoost);
            m.SetFloat("_Wobble", wobble);
            m.SetFloat("_WobbleFreq", freq);
            m.SetFloat("_WobbleSpeed", speed);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>투명 배경 그림을 그대로 그리는 재질 (돌처럼 어두운 색도 탁해지지 않게)</summary>
        static Material AlphaSpriteMat(string name, string tex)
        {
            var path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("Sprites/Default")) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.mainTexture = Tex(tex, true);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Texture2D Tex(string name, bool alpha)
        {
            var path = $"{TexDir}/{name}.png";
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Default;
                imp.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                imp.alphaIsTransparency = alpha;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.mipmapEnabled = false;
                imp.maxTextureSize = 1024;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static ParticleSystem TrailPrefab(string name, Color a, Color b, float sizeMul)
        {
            var path = $"{FxDir}/{name}.prefab";
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(path))
            {
                AssetDatabase.CopyAsset($"{FxDir}/FX_SmokeTrail.prefab", path);
                var go = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var ps = go.GetComponent<ParticleSystem>();
                    var main = ps.main;
                    main.startColor = new ParticleSystem.MinMaxGradient(a, b);
                    main.startSizeMultiplier *= sizeMul;
                    PrefabUtility.SaveAsPrefabAsset(go, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(go); }
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<ParticleSystem>();
        }

        static void Recolor(Transform t, Color a, Color? b)
        {
            if (!t) return;
            var ps = t.GetComponent<ParticleSystem>();
            if (!ps) return;
            var main = ps.main;
            main.startColor = b.HasValue ? new ParticleSystem.MinMaxGradient(a, b.Value) : new ParticleSystem.MinMaxGradient(a);
        }
    }
}

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 一键生成完整的 2D 平台跳跃关卡：相机、玩家、地形、金币、史莱姆、尖刺、
/// 移动平台、检查点、终点旗、视差背景和 HUD。
/// 菜单：Platformer / 2 - Build Game Scene
/// </summary>
public static class PlatformerLevelBuilder
{
    const string ScenePath = "Assets/Platformer/Scenes/Platformer.unity";
    const string ArtFolder = "Assets/Platformer/Art";
    const float CameraSize = 5.2f;

    // 关卡数值（都在世界单位里）
    const float LevelMinX = -15f;
    const float LevelMaxX = 107f;
    const float KillY = -9f;

    static int groundLayer;
    static int groundMask;
    static Sprite ArtDirt, ArtGrass, ArtCoin, ArtSpike, ArtSlime, ArtFlag, ArtCheckpoint,
                  ArtCloud, ArtBush, ArtHillFar, ArtHillNear, ArtSky, ArtPlayer;

    class Block
    {
        public float centerX;
        public float top;
        public float width;
        public float height;
        public bool oneWay;
        public bool grassy = true;

        public Block(float centerX, float top, float width, float height, bool oneWay = false, bool grassy = true)
        {
            this.centerX = centerX;
            this.top = top;
            this.width = width;
            this.height = height;
            this.oneWay = oneWay;
            this.grassy = grassy;
        }
    }

    [MenuItem("Platformer/2 - Build Game Scene")]
    public static void BuildFromMenu()
    {
        Build();
        EditorUtility.DisplayDialog("Platformer", "已生成并打开关卡：" + ScenePath + "\n按 Play 即可试玩。", "好");
    }

    public const string BuildVersion = "3";

    public static void Build()
    {
        Scene currentScene = EditorSceneManager.GetActiveScene();
        if (currentScene.isDirty) EditorSceneManager.SaveOpenScenes();

        PlatformerArt.GenerateAll();
        LoadArt();

        groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer < 0) groundLayer = 6;
        groundMask = 1 << groundLayer;

        // 直接用单场景模式新建，避免与已打开的 Platformer 场景争同一路径
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject world = new GameObject("World");

        // ---------- 玩家 ----------
        Vector3 spawn = new Vector3(-10.5f, 1.4f, 0f);
        GameObject player = CreatePlayer(spawn);

        // ---------- 相机 ----------
        PlatformerCamera cameraFollow = CreateCamera(player.transform);
        Camera gameCamera = cameraFollow.GetComponent<Camera>();

        // ---------- 2D 全局光 ----------
        CreateGlobalLight(world.transform);

        // ---------- 背景 ----------
        CreateBackground(world.transform, gameCamera);

        // ---------- 地形 ----------
        GameObject level = new GameObject("Level");
        level.transform.SetParent(world.transform, false);

        Block[] blocks =
        {
            // 起始平台
            new Block(-3f, 0f, 22f, 2f),
            // 第二个平台（史莱姆 + 检查点）
            new Block(18.5f, 0f, 13f, 2f),
            // 第三个平台（尖刺）
            new Block(43f, 0f, 12f, 2f),
            // 移动平台之后的大平台
            new Block(73f, 0f, 14f, 2f),
            // 终点高台
            new Block(98.5f, 6.5f, 15f, 2f),

            // 坑底（不放草）
            new Block(10f, -2f, 4.6f, 2f, false, false),
            new Block(31f, -2f, 13f, 2f, false, false),
            new Block(57.5f, -2f, 18f, 2f, false, false),
            new Block(85.5f, -2f, 12f, 2f, false, false),

            // 单向平台（可从下往上跳穿）
            new Block(15f, 2.4f, 4f, 0.5f, true),

            // 浮空平台
            new Block(27f, 1.8f, 3.5f, 0.6f),
            new Block(31f, 3.6f, 3.5f, 0.6f),
            new Block(35f, 2.2f, 4f, 0.6f),

            // 尖刺坑上的落脚点
            new Block(50.5f, 2.0f, 2.6f, 0.6f),
            new Block(53.5f, 2.6f, 2.6f, 0.6f),

            // 最后一段攀爬
            new Block(82f, 2.4f, 3f, 0.6f),
            new Block(85.5f, 4.4f, 3f, 0.6f),
            new Block(89f, 6.4f, 3f, 0.6f),
        };

        for (int i = 0; i < blocks.Length; i++)
            CreateBlock(level.transform, "Platform_" + i, blocks[i]);

        // ---------- 尖刺 ----------
        CreateSpikes(level.transform, 10f, -2f, 4.6f);
        CreateSpikes(level.transform, 31f, -2f, 13f);
        CreateSpikes(level.transform, 57.5f, -2f, 18f);
        CreateSpikes(level.transform, 85.5f, -2f, 12f);
        CreateSpikes(level.transform, 46.5f, 0f, 2f);

        // ---------- 移动平台 ----------
        CreateMovingPlatform(level.transform, new Vector3(57f, 1.6f, 0f), new Vector2(5.5f, 0f), 2.6f, 0.5f, 0.22f);

        // ---------- 金币 ----------
        Vector3[] coins =
        {
            new Vector3(-9f, 1.1f, 0f), new Vector3(-7.5f, 1.1f, 0f), new Vector3(-6f, 1.1f, 0f),
            new Vector3(9f, 2.0f, 0f), new Vector3(10f, 2.7f, 0f), new Vector3(11f, 2.0f, 0f),
            new Vector3(14f, 3.2f, 0f), new Vector3(16f, 3.2f, 0f),
            new Vector3(17f, 1.1f, 0f),
            new Vector3(31f, 4.4f, 0f),
            new Vector3(35f, 3.0f, 0f),
            new Vector3(39f, 1.1f, 0f), new Vector3(41f, 1.1f, 0f),
            new Vector3(46.5f, 2.0f, 0f),
            new Vector3(50.5f, 2.8f, 0f), new Vector3(53.5f, 3.4f, 0f),
            new Vector3(68f, 1.1f, 0f), new Vector3(70f, 1.1f, 0f), new Vector3(72f, 1.1f, 0f),
            new Vector3(82f, 3.2f, 0f), new Vector3(85.5f, 5.2f, 0f), new Vector3(89f, 7.2f, 0f),
            new Vector3(94f, 7.6f, 0f), new Vector3(96.5f, 7.6f, 0f), new Vector3(99f, 7.6f, 0f),
        };

        GameObject items = new GameObject("Items");
        items.transform.SetParent(world.transform, false);
        for (int i = 0; i < coins.Length; i++)
            CreateCoin(items.transform, coins[i]);

        // ---------- 敌人 ----------
        GameObject enemies = new GameObject("Enemies");
        enemies.transform.SetParent(world.transform, false);
        CreateSlime(enemies.transform, new Vector3(20f, 0.6f, 0f), 3f);
        CreateSlime(enemies.transform, new Vector3(40f, 0.6f, 0f), 3f);
        CreateSlime(enemies.transform, new Vector3(70f, 0.6f, 0f), 2.5f);
        CreateSlime(enemies.transform, new Vector3(75.5f, 0.6f, 0f), 2.5f);

        // ---------- 检查点 ----------
        GameObject checkpoints = new GameObject("Checkpoints");
        checkpoints.transform.SetParent(world.transform, false);
        CreateCheckpoint(checkpoints.transform, new Vector3(23.5f, 0f, 0f));
        CreateCheckpoint(checkpoints.transform, new Vector3(67.5f, 0f, 0f));
        CreateCheckpoint(checkpoints.transform, new Vector3(92.5f, 6.5f, 0f));

        // ---------- 终点 ----------
        CreateGoal(world.transform, new Vector3(103f, 6.5f, 0f));

        // ---------- 管理器 ----------
        GameObject game = new GameObject("Game");
        game.transform.SetParent(world.transform, false);
        SfxPlayer sfx = game.AddComponent<SfxPlayer>();
        GameManager manager = game.AddComponent<GameManager>();
        manager.player = player.GetComponent<PlayerController2D>();
        manager.gameCamera = cameraFollow;
        manager.totalCoins = coins.Length;
        manager.spawnPoint = spawn;
        manager.killY = KillY;
        cameraFollow.target = player.transform;

        // ---------- HUD ----------
        HUDController hud = CreateHud(world.transform);
        manager.hud = hud;

        // ---------- 保存 ----------
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings();

        Debug.Log(string.Format("[Platformer] 关卡已生成：{0} 个平台，{1} 枚金币，{2} 个敌人。",
            blocks.Length, coins.Length, 4));
    }

    // ---------------------------------------------------------------- 资源

    static void LoadArt()
    {
        ArtPlayer = LoadSprite("player");
        ArtSlime = LoadSprite("slime");
        ArtCoin = LoadSprite("coin");
        ArtSpike = LoadSprite("spike");
        ArtDirt = LoadSprite("dirt");
        ArtGrass = LoadSprite("grass");
        ArtFlag = LoadSprite("flag");
        ArtCheckpoint = LoadSprite("checkpoint");
        ArtCloud = LoadSprite("cloud");
        ArtBush = LoadSprite("bush");
        ArtHillFar = LoadSprite("hill_far");
        ArtHillNear = LoadSprite("hill_near");
        ArtSky = LoadSprite("sky");
    }

    static Sprite LoadSprite(string name)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/" + name + ".png");
        if (sprite == null) Debug.LogError("[Platformer] 加载美术失败：" + name);
        return sprite;
    }

    // ---------------------------------------------------------------- 玩家 / 相机

    static GameObject CreatePlayer(Vector3 spawn)
    {
        GameObject root = new GameObject("Player");
        root.transform.position = spawn;

        Rigidbody2D body = root.AddComponent<Rigidbody2D>();
        body.gravityScale = 3f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        BoxCollider2D box = root.AddComponent<BoxCollider2D>();
        box.size = new Vector2(0.78f, 1.16f);
        box.offset = Vector2.zero;
        box.sharedMaterial = GetPlayerPhysicsMaterial();

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);

        SpriteRenderer sprite = visual.AddComponent<SpriteRenderer>();
        sprite.sprite = ArtPlayer;
        sprite.sortingOrder = 10;

        PlayerController2D controller = root.AddComponent<PlayerController2D>();
        controller.visual = visual.transform;
        controller.spriteRenderer = sprite;
        controller.visualScale = 1.3f;
        controller.groundMask = groundMask;
        controller.killY = KillY;
        controller.jumpHeight = 3.2f;
        controller.gravityScale = 3f;
        controller.moveSpeed = 6.5f;

        return root;
    }

    static PhysicsMaterial2D GetPlayerPhysicsMaterial()
    {
        string path = "Assets/Platformer/PlayerPhysics.physicsMaterial2D";
        PhysicsMaterial2D material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
        if (material == null)
        {
            material = new PhysicsMaterial2D("PlayerPhysics");
            AssetDatabase.CreateAsset(material, path);
        }
        material.friction = 0f;
        material.bounciness = 0f;
        EditorUtility.SetDirty(material);
        return material;
    }

    static PlatformerCamera CreateCamera(Transform target)
    {
        GameObject go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        go.transform.position = new Vector3(-9f, 3f, -10f);

        Camera cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = CameraSize;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(0x9F, 0xD8, 0xF2, 255);
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 100f;

        go.AddComponent<AudioListener>();

        PlatformerCamera follow = go.AddComponent<PlatformerCamera>();
        follow.target = target;
        follow.offset = new Vector2(0f, 1.2f);
        follow.levelMinX = LevelMinX;
        follow.levelMaxX = LevelMaxX;
        follow.levelMinY = 1.8f;
        follow.levelMaxY = 12f;
        follow.SnapToTarget();

        return follow;
    }

    // ---------------------------------------------------------------- 背景

    static void CreateGlobalLight(Transform parent)
    {
        GameObject go = new GameObject("Global Light 2D");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(0f, 0f, 0f);

        Light2D light = go.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.color = Color.white;
        light.intensity = 1f;
    }
    static void CreateBackground(Transform parent, Camera cam)
    {
        GameObject background = new GameObject("Background");
        background.transform.SetParent(parent, false);

        // 天空
        GameObject sky = new GameObject("Sky");
        sky.transform.SetParent(background.transform, false);
        SpriteRenderer skySprite = sky.AddComponent<SpriteRenderer>();
        skySprite.sprite = ArtSky;
        skySprite.sortingOrder = -300;
        SkyBackground skyScript = sky.AddComponent<SkyBackground>();
        skyScript.targetCamera = cam;
        skyScript.Refresh();

        // 云
        CreateParallaxStrip(background.transform, "Clouds", ArtCloud, new Vector2(46f, 6.4f), new Vector2(260f, 1.375f), -280, 0.22f, cam.transform);
        CreateParallaxStrip(background.transform, "CloudsMid", ArtCloud, new Vector2(38f, 8.2f), new Vector2(260f, 1.375f), -282, 0.17f, cam.transform);
        CreateParallaxStrip(background.transform, "CloudsBack", ArtCloud, new Vector2(30f, 10.2f), new Vector2(260f, 1.375f), -285, 0.14f, cam.transform);

        // 远山 / 近山（贴图：远山 512x288 = 16x9，近山 512x208 = 16x6.5，底部都在 -3.2）
        CreateParallaxStrip(background.transform, "HillsFar", ArtHillFar, new Vector2(46f, 1.3f), new Vector2(262f, 9f), -250, 0.42f, cam.transform);
        CreateParallaxStrip(background.transform, "HillsNear", ArtHillNear, new Vector2(46f, 0.05f), new Vector2(262f, 6.5f), -220, 0.68f, cam.transform);

        // 地面装饰
        Sprite bush = LoadSprite("bush");
        float[] bushXs = { -12f, -4f, 2f, 6f, 15f, 21f, 39f, 47f, 68f, 77f, 93f, 101f };
        for (int i = 0; i < bushXs.Length; i++)
        {
            GameObject decor = new GameObject("Bush");
            decor.transform.SetParent(background.transform, false);
            decor.transform.position = new Vector3(bushXs[i], 0.32f, 0f);
            SpriteRenderer renderer = decor.AddComponent<SpriteRenderer>();
            renderer.sprite = bush;
            renderer.sortingOrder = 2;
            renderer.flipX = i % 2 == 0;
        }
    }

    static void CreateParallaxStrip(Transform parent, string name, Sprite sprite, Vector2 position,
                                    Vector2 size, int sortingOrder, float factor, Transform cameraTransform)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(position.x, position.y, 0f);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.tileMode = SpriteTileMode.Continuous;
        renderer.size = size;
        renderer.sortingOrder = sortingOrder;

        ParallaxLayer parallax = go.AddComponent<ParallaxLayer>();
        parallax.cameraTransform = cameraTransform;
        parallax.factorX = factor;
        parallax.factorY = 0.12f;
    }

    // ---------------------------------------------------------------- 地形

    static GameObject CreateBlock(Transform parent, string name, Block block)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(block.centerX, block.top - block.height * 0.5f, 0f);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = ArtDirt;
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.tileMode = SpriteTileMode.Continuous;
        renderer.size = new Vector2(block.width, block.height);
        renderer.sortingOrder = 0;

        if (block.grassy)
        {
            GameObject cap = new GameObject("Grass");
            cap.transform.SetParent(go.transform, false);
            cap.transform.localPosition = new Vector3(0f, block.height * 0.5f, 0f);

            SpriteRenderer capSprite = cap.AddComponent<SpriteRenderer>();
            capSprite.sprite = ArtGrass;
            capSprite.drawMode = SpriteDrawMode.Tiled;
            capSprite.tileMode = SpriteTileMode.Continuous;
            capSprite.size = new Vector2(block.width, 0.375f);
            capSprite.sortingOrder = 1;
        }

        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(block.width, block.height);
        box.offset = Vector2.zero;
        go.layer = groundLayer;

        if (block.oneWay)
        {
            box.usedByEffector = true;
            PlatformEffector2D effector = go.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.useSideFriction = false;
            effector.useSideBounce = false;
            effector.surfaceArc = 160f;
        }

        return go;
    }

    static void CreateSpikes(Transform parent, float centerX, float baseTop, float width)
    {
        GameObject go = new GameObject("Spikes");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(centerX, baseTop + 0.25f, 0f);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = ArtSpike;
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.tileMode = SpriteTileMode.Continuous;
        renderer.size = new Vector2(width, 0.5f);
        renderer.sortingOrder = 3;

        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(width - 0.12f, 0.34f);
        box.offset = new Vector2(0f, -0.05f);
        box.isTrigger = true;

        go.AddComponent<Hazard>();
    }

    static void CreateMovingPlatform(Transform parent, Vector3 position, Vector2 offset, float width, float height, float speed)
    {
        GameObject go = new GameObject("MovingPlatform");
        go.transform.SetParent(parent, false);
        go.transform.position = position;

        Rigidbody2D body = go.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.freezeRotation = true;

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = ArtDirt;
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.tileMode = SpriteTileMode.Continuous;
        renderer.size = new Vector2(width, height);
        renderer.sortingOrder = 0;

        GameObject cap = new GameObject("Grass");
        cap.transform.SetParent(go.transform, false);
        cap.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
        SpriteRenderer capSprite = cap.AddComponent<SpriteRenderer>();
        capSprite.sprite = ArtGrass;
        capSprite.drawMode = SpriteDrawMode.Tiled;
        capSprite.tileMode = SpriteTileMode.Continuous;
        capSprite.size = new Vector2(width, 0.375f);
        capSprite.sortingOrder = 1;

        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(width, height);
        go.layer = groundLayer;

        MovingPlatform platform = go.AddComponent<MovingPlatform>();
        platform.offset = offset;
        platform.speed = speed;
    }

    // ---------------------------------------------------------------- 道具 / 敌人

    static void CreateCoin(Transform parent, Vector3 position)
    {
        GameObject go = new GameObject("Coin");
        go.transform.SetParent(parent, false);
        go.transform.position = position;

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = ArtCoin;
        renderer.sortingOrder = 6;

        CircleCollider2D trigger = go.AddComponent<CircleCollider2D>();
        trigger.radius = 0.28f;
        trigger.isTrigger = true;

        go.AddComponent<CoinPickup>();
    }

    static void CreateSlime(Transform parent, Vector3 position, float patrol)
    {
        GameObject go = new GameObject("Slime");
        go.transform.SetParent(parent, false);
        go.transform.position = position;

        Rigidbody2D body = go.AddComponent<Rigidbody2D>();
        body.gravityScale = 1f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(0.6f, 0.42f);
        box.offset = new Vector2(0f, -0.05f);
        box.sharedMaterial = GetPlayerPhysicsMaterial();

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = ArtSlime;
        renderer.sortingOrder = 8;

        SlimeEnemy enemy = go.AddComponent<SlimeEnemy>();
        enemy.patrolDistance = patrol;
        enemy.groundMask = groundMask;
        enemy.speed = 2.1f;
    }

    static void CreateCheckpoint(Transform parent, Vector3 groundPosition)
    {
        GameObject go = new GameObject("Checkpoint");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(groundPosition.x, groundPosition.y + 0.625f, 0f);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = ArtCheckpoint;
        renderer.sortingOrder = 4;

        BoxCollider2D trigger = go.AddComponent<BoxCollider2D>();
        trigger.size = new Vector2(0.9f, 2.2f);
        trigger.offset = new Vector2(0f, 0.6f);
        trigger.isTrigger = true;

        Checkpoint checkpoint = go.AddComponent<Checkpoint>();
        checkpoint.flag = go.transform;
        checkpoint.flagRenderer = renderer;
        checkpoint.respawnOffset = new Vector3(0f, 1.2f, 0f);
    }

    static void CreateGoal(Transform parent, Vector3 groundPosition)
    {
        GameObject go = new GameObject("Goal");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(groundPosition.x, groundPosition.y + 0.78f, 0f);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = ArtFlag;
        renderer.sortingOrder = 4;

        BoxCollider2D trigger = go.AddComponent<BoxCollider2D>();
        trigger.size = new Vector2(1.4f, 3f);
        trigger.offset = new Vector2(0f, 0.6f);
        trigger.isTrigger = true;

        LevelGoal goal = go.AddComponent<LevelGoal>();
        goal.flagVisual = go.transform;
    }

    // ---------------------------------------------------------------- HUD

    static HUDController CreateHud(Transform parent)
    {
        GameObject canvasGo = new GameObject("HUD");
        canvasGo.transform.SetParent(parent, false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        HUDController hud = canvasGo.AddComponent<HUDController>();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 金币图标
        GameObject coinIcon = new GameObject("CoinIcon", typeof(RectTransform));
        coinIcon.transform.SetParent(canvasGo.transform, false);
        RectTransform coinRect = coinIcon.GetComponent<RectTransform>();
        coinRect.anchorMin = new Vector2(0f, 1f);
        coinRect.anchorMax = new Vector2(0f, 1f);
        coinRect.pivot = new Vector2(0f, 1f);
        coinRect.anchoredPosition = new Vector2(40f, -34f);
        coinRect.sizeDelta = new Vector2(56f, 56f);

        Image coinImage = coinIcon.AddComponent<Image>();
        coinImage.sprite = ArtCoin;
        coinImage.preserveAspect = true;
        coinImage.raycastTarget = false;

        hud.coinText = CreateText(canvasGo.transform, "CoinText", "0 / 0", 46,
            new Color(1f, 1f, 1f), TextAnchor.MiddleLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(112f, -62f), new Vector2(360f, 56f), font);

        hud.deathText = CreateText(canvasGo.transform, "DeathText", "死亡 0", 40,
            new Color(1f, 1f, 1f, 0.95f), TextAnchor.MiddleRight,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -62f), new Vector2(320f, 56f), font);

        hud.timeText = CreateText(canvasGo.transform, "TimeText", "00:00", 40,
            new Color(1f, 1f, 1f, 0.95f), TextAnchor.MiddleRight,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -112f), new Vector2(320f, 56f), font);

        Text hint = CreateText(canvasGo.transform, "Hint",
            "A / D 或 ← → 移动      空格 跳跃      S + 空格 下平台      R 重新开始", 34,
            new Color(1f, 1f, 1f, 0.95f), TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 54f), new Vector2(1400f, 60f), font);
        CanvasGroup hintGroup = hint.gameObject.AddComponent<CanvasGroup>();
        hud.hintText = hint;
        hud.hintGroup = hintGroup;

        // 通关面板
        GameObject winPanel = new GameObject("WinPanel", typeof(RectTransform));
        winPanel.transform.SetParent(canvasGo.transform, false);
        RectTransform winRect = winPanel.GetComponent<RectTransform>();
        winRect.anchorMin = Vector2.zero;
        winRect.anchorMax = Vector2.one;
        winRect.offsetMin = Vector2.zero;
        winRect.offsetMax = Vector2.zero;

        Image winBackground = winPanel.AddComponent<Image>();
        winBackground.color = new Color(0.05f, 0.07f, 0.13f, 0.78f);
        winBackground.raycastTarget = false;

        hud.winTitle = CreateText(winPanel.transform, "WinTitle", "关卡完成！", 100,
            new Color(1f, 0.86f, 0.36f), TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 210f), new Vector2(1200f, 160f), font);

        hud.winStats = CreateText(winPanel.transform, "WinStats", "", 44,
            Color.white, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(1100f, 420f), font);

        winPanel.SetActive(false);
        hud.winPanel = winPanel;

        return hud;
    }

    static Text CreateText(Transform parent, string name, string content, int fontSize, Color color,
                           TextAnchor alignment, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Font font)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Text text = go.AddComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        Shadow shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        shadow.effectDistance = new Vector2(2f, -2f);

        return text;
    }

    // ---------------------------------------------------------------- 构建设置

    static void AddSceneToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        for (int i = 0; i < scenes.Count; i++)
            if (scenes[i].path == ScenePath) return;

        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}

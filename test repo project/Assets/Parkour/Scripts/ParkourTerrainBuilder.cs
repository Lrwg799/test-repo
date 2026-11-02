using UnityEngine;

/// <summary>跑酷地形段类型</summary>
public enum TerrainType
{
    Flat = 0,          // 1. 平地
    FlatWithGap = 1,   // 2. 中央带坑洞的平地
    UpSlope = 2,       // 3. 上坡（45° 短坡，前后平地）
    DownSlope = 3,     // 4. 下坡（45° 短坡，前后平地）
    Platform = 4,      // 5. 平台（比一层高一层的独立悬空平台）
    Empty = 5          // 6. 空隙（纯空）
}

/// <summary>
/// 跑酷地形段生成器。
///
/// 只管「造一段」这件事：给定类型、左端 X、地面高度，生成对应的一堆方块。
/// 滚动、回收、随机选型由 ParkourRunner 负责。
///
/// 段内坐标以【该段左端 + 地面高度】为原点，x 向右、y 向上。
/// </summary>
public class ParkourTerrainBuilder : MonoBehaviour
{
    [Header("尺寸（世界单位）")]
    [Tooltip("每段宽度")]
    public float segmentWidth = 6f;

    [Tooltip("地形块厚度（地面块会铺到屏幕下方，看起来是实心土地）")]
    public float thickness = 0.6f;

    [Tooltip("地面下方再补多厚的土体，用来盖住段与段接缝处漏出来的空隙")]
    public float baseFillDepth = 10f;

    [Tooltip("一层的高度（平台高度 / 坡度总升高）")]
    public float levelHeight = 1.5f;

    [Tooltip("坑洞宽度（类型 2 用）")]
    public float gapWidth = 2f;

    [Tooltip("45° 坡的水平投影长度（等于升高，保持 45°）")]
    public float slopeRun = 1.5f;

    [Tooltip("平台两侧各留多少空隙，让它悬空独立")]
    public float platformMargin = 1f;

    [Tooltip("平台下方有地面的概率（0 = 永远是坑洞，1 = 永远有地面）")]
    [Range(0f, 1f)]
    public float platformGroundChance = 0.5f;

    [Header("随机规则")]
    [Tooltip("可随机的类型池")]
    public TerrainType[] pool = new TerrainType[]
    {
        TerrainType.Flat,
        TerrainType.FlatWithGap,
        TerrainType.UpSlope,
        TerrainType.DownSlope,
        TerrainType.Platform,
        TerrainType.Empty,
    };

    [Tooltip("地形最高处（超过就不再随机上坡/平台）")]
    public float maxY = 3f;

    [Tooltip("地形最低处（低于就不再随机下坡）")]
    public float minY = -6f;

    [Tooltip("不允许连续出现两个空隙（否则必死）")]
    public bool forbidDoubleEmpty = true;

    [Tooltip("与 Pool 一一对应的出现权重（越大越常见，0 = 不出现）")]
    public int[] poolWeights = new int[] { 3, 2, 1, 1, 1, 1 };

    [Header("外观")]
    [Tooltip("方块贴图（1x1 世界单位的纯白图）")]
    public Sprite blockSprite;

    public Color flatColor = new Color(0.20f, 0.25f, 0.33f);
    public Color slopeColor = new Color(0.24f, 0.30f, 0.40f);
    public Color platformColor = new Color(0.28f, 0.34f, 0.45f);

    [Header("可拾取的月亮碎片")]
    [Tooltip("碎片贴图（留空则不生成）")]
    public Sprite coinSprite;

    [Tooltip("是否在每段地形上放碎片")]
    public bool spawnCoins = true;

    [Tooltip("每段放几个碎片（1~3，越多白天能捡到的越多）")]
    public int coinsPerSegment = 3;

    [Tooltip("碎片离地面的高度")]
    public float coinHeight = 1.15f;

    [Tooltip("碎片总直径（世界单位）")]
    public float coinSize = 0.55f;

    public Color coinColor = new Color(1f, 0.86f, 0.30f);

    [Header("随机浮空平台")]
    [Tooltip("是否生成浮空平台")]
    public bool spawnFloatingPlatforms = true;

    [Tooltip("每段出现浮空平台的概率")]
    [Range(0f, 1f)]
    public float floatingPlatformChance = 0.4f;

    [Tooltip("平台离下方地面的高度（固定值）。跳跃高度 2.21，别超过 2.0")]
    public float floatingPlatformHeight = 1.8f;

    [Tooltip("平台宽度下限（太短会站不住）")]
    public float floatingPlatformWidthMin = 2.8f;

    [Tooltip("平台宽度上限（每段在这个区间内随机）")]
    public float floatingPlatformWidthMax = 4.6f;

    [Tooltip("平台下方是空地的概率（其余情况下方仍有地面，掉下去不死）")]
    [Range(0f, 1f)]
    public float floatingPlatformPitChance = 0.5f;

    [Tooltip("平台厚度")]
    public float floatingPlatformThickness = 0.5f;

    [Tooltip("密集的一侧放几颗碎片（另一侧只放剩下的 1 颗）")]
    public int denseCoinCount = 3;

    [Tooltip("密集侧的碎片间距（越小越挤）")]
    public float denseCoinSpacing = 0.72f;

    [Header("长度约束")]
    [Tooltip("地面/平台的最小长度，单位 = 主控角色宽度。6 表示至少 6 个角色宽。0 = 不检查")]
    public float minLengthInCharacterWidths = 6f;

    [Tooltip("碎片之间的固定横向间距")]
    public float coinSpacing = 0.9f;

    [Header("冒险奖励")]
    [Tooltip("普通碎片的价值")]
    public int normalCoinValue = 1;

    [Tooltip("「下方是空地」的平台段上，碎片价值翻倍（走危险路线的奖励）")]
    public int riskyCoinValue = 2;

    /// <summary>这个序号上的碎片值多少</summary>
    int CoinValueAt(int index, int platformCoins, bool pitPlatform)
    {
        // 只有「下方是空地的平台」上的碎片才算冒险奖励
        if (pitPlatform && index < platformCoins) return Mathf.Max(1, riskyCoinValue);
        return Mathf.Max(1, normalCoinValue);
    }

    [Tooltip("碎片总数（有浮空平台的段会多给 1 颗）")]
    public int coinsPerFloatingSegment = 4;

    [Header("空隙")]
    [Tooltip("纯空隙段中间挖掉多宽。剩下的宽度会作为两端的窄边，和相邻段地面连成落点")]
    public float emptyGapWidth = 4.2f;

    // 每段固定的方块槽数量（不再销毁重建，回收时只改槽的参数）
    private const int MaxBlocks = 6;
    private const int MaxCoins = 6;
    private int slotCursor;

    /// <summary>该类型段的「出口高度」（下一段从这里接着铺）</summary>
    public float ExitY(TerrainType type, float y)
    {
        if (type == TerrainType.UpSlope) return y + levelHeight;
        if (type == TerrainType.DownSlope) return y - levelHeight;
        return y;
    }

    /// <summary>按当前高度和上一段类型，随机挑一个不会卡死的类型</summary>
    public TerrainType PickType(float y, TerrainType previous)
    {
        // 只在「空隙」之后强制接一段平地 —— 保证玩家有落脚点。
        // （抬高地面之后不强制，否则平地会占掉太多比例，地形变化就不频繁了）
        if (previous == TerrainType.Empty)
        {
            return TerrainType.Flat;
        }

        var candidates = new System.Collections.Generic.List<TerrainType>();
        var weights = new System.Collections.Generic.List<int>();
        int total = 0;

        for (int i = 0; i < pool.Length; i++)
        {
            TerrainType t = pool[i];

            if (t == TerrainType.UpSlope && y + levelHeight > maxY) continue;
            if (t == TerrainType.Platform && y + levelHeight > maxY) continue;
            if (t == TerrainType.DownSlope && y - levelHeight < minY) continue;

            if (forbidDoubleEmpty && t == TerrainType.Empty && previous == TerrainType.Empty) continue;

            // 「空隙」和「平台」相邻会拼出 6 + 1 = 7 单位的断口，超出跳跃能力 → 禁止
            if (t == TerrainType.Empty && previous == TerrainType.Platform) continue;
            if (t == TerrainType.Platform && previous == TerrainType.Empty) continue;

            int w = (poolWeights != null && i < poolWeights.Length) ? poolWeights[i] : 1;
            if (w <= 0) continue;

            candidates.Add(t);
            weights.Add(w);
            total += w;
        }

        if (candidates.Count == 0 || total <= 0) return TerrainType.Flat;

        int roll = Random.Range(0, total);
        for (int i = 0; i < candidates.Count; i++)
        {
            roll -= weights[i];
            if (roll < 0) return candidates[i];
        }
        return candidates[candidates.Count - 1];
    }

    /// <summary>新建一个段物体并铺好方块</summary>
    public GameObject CreateSegment(int index, TerrainType type, float x0, float y)
    {
        var seg = new GameObject();
        seg.transform.SetParent(transform, false);

        RebuildSegment(seg, type, x0, y, index);
        return seg;
    }

    /// <summary>把已有的段重新铺成新类型（回收复用）</summary>
    public void RebuildSegment(GameObject seg, TerrainType type, float x0, float y, int index)
    {
        // 不再销毁重建（回收时反复销毁/新建碰撞体会掉帧），改成复用固定的方块槽
        EnsureSlots(seg);

        seg.name = index.ToString("00") + "_" + type;
        seg.transform.localPosition = new Vector3(x0, y, 0f);

        BuildContent(seg, type);
    }

    /// <summary>每个段固定 MaxBlocks 个方块槽 + 1 个碎片槽；回收时只改参数，不销毁重建</summary>
    void EnsureSlots(GameObject seg)
    {
        for (int i = 0; i < MaxBlocks; i++)
        {
            string n = "Block_" + i;
            if (seg.transform.Find(n) != null) continue;

            var go = new GameObject(n);
            go.transform.SetParent(seg.transform, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = blockSprite;

            go.AddComponent<BoxCollider2D>();
            go.SetActive(false);
        }

        for (int i = 0; i < MaxCoins; i++)
        {
            string n = "Coin_" + i;
            if (seg.transform.Find(n) != null) continue;

            var c = new GameObject(n);
            c.transform.SetParent(seg.transform, false);

            var sr = c.AddComponent<SpriteRenderer>();
            sr.sprite = coinSprite;
            sr.color = coinColor;
            sr.sortingOrder = 5;

            c.AddComponent<MoonCoin>();
            c.SetActive(false);
        }
    }

    void BuildContent(GameObject seg, TerrainType type)
    {
        slotCursor = 0;

        // 这一段要不要生成浮空平台？
        // 斜坡段（UpSlope / DownSlope）不放 —— 斜坡块是旋转的大块，
        // 平台容易和它的斜边叠在一起，看起来像"长在斜面上"
        bool canHavePlatform = type == TerrainType.Flat
                               || type == TerrainType.FlatWithGap
                               || type == TerrainType.Platform;

        bool hasPlatform = spawnFloatingPlatforms && canHavePlatform
                           && Random.value < floatingPlatformChance;

        // 平台下方是不是空地（是 → 掉下去失败；否 → 底下还有地面接着）
        bool pitPlatform = hasPlatform && Random.value < floatingPlatformPitChance;

        // 碎片这次密集在哪一边
        bool denseOnPlatform = Random.value < 0.5f;

        float platX = segmentWidth * 0.5f;
        float platTop = 0f;

        // 平台长度随机，但不允许短于 MinPieceLength（默认 6 个角色宽）
        float minLen = MinPieceLength();
        float platWidth = Mathf.Max(minLen,
                                    Random.Range(floatingPlatformWidthMin, floatingPlatformWidthMax));

        // ---- 浮空平台 ----
        if (hasPlatform)
        {
            platTop = SurfaceYAt(type, platX) + floatingPlatformHeight;

            AddBlock(seg, platX, platTop - floatingPlatformThickness * 0.5f,
                     platWidth, floatingPlatformThickness, 0f,
                     platformColor, "FloatPlatform");
        }

        // ---- 地面（下方是空地的段直接跳过）----
        if (!pitPlatform)
        {
            switch (type)
            {
            // ---------- 1. 平地 ----------
            case TerrainType.Flat:
                AddBlock(seg, segmentWidth * 0.5f, -thickness * 0.5f,
                         segmentWidth, thickness, 0f, flatColor, "Ground");
                break;

            // ---------- 2. 中央带坑洞的平地 ----------
            case TerrainType.FlatWithGap:
            {
                float side = (segmentWidth - gapWidth) * 0.5f;
                AddBlock(seg, side * 0.5f, -thickness * 0.5f,
                         side, thickness, 0f, flatColor, "Ground_Left");
                AddBlock(seg, segmentWidth - side * 0.5f, -thickness * 0.5f,
                         side, thickness, 0f, flatColor, "Ground_Right");
                break;
            }

            // ---------- 3. 上坡 ----------
            case TerrainType.UpSlope:
            {
                float len = Mathf.Sqrt(slopeRun * slopeRun + levelHeight * levelHeight);
                float backFlat = segmentWidth - slopeRun * 2f;

                AddBlock(seg, slopeRun * 0.5f, -thickness * 0.5f,
                         slopeRun, thickness, 0f, flatColor, "Ground_Before");

                // 45° 坡：从 (slopeRun, 0) 升到 (slopeRun*2, levelHeight)
                Vector2 offset = new Vector2(0.7071f, -0.7071f) * (thickness * 0.5f);
                AddBlock(seg, slopeRun * 1.5f + offset.x, levelHeight * 0.5f + offset.y,
                         len, thickness, 45f, slopeColor, "Slope");

                AddBlock(seg, slopeRun * 2f + backFlat * 0.5f, levelHeight - thickness * 0.5f,
                         backFlat, thickness, 0f, flatColor, "Ground_After");
                break;
            }

            // ---------- 4. 下坡 ----------
            case TerrainType.DownSlope:
            {
                float len = Mathf.Sqrt(slopeRun * slopeRun + levelHeight * levelHeight);
                float backFlat = segmentWidth - slopeRun * 2f;

                AddBlock(seg, slopeRun * 0.5f, -thickness * 0.5f,
                         slopeRun, thickness, 0f, flatColor, "Ground_Before");

                Vector2 offset = new Vector2(-0.7071f, -0.7071f) * (thickness * 0.5f);
                AddBlock(seg, slopeRun * 1.5f + offset.x, -levelHeight * 0.5f + offset.y,
                         len, thickness, -45f, slopeColor, "Slope");

                AddBlock(seg, slopeRun * 2f + backFlat * 0.5f, -levelHeight - thickness * 0.5f,
                         backFlat, thickness, 0f, flatColor, "Ground_After");
                break;
            }

            // ---------- 5. 抬高地面（整段抬高一层的实心地块，不再是悬空平台） ----------
            case TerrainType.Platform:
            {
                // 顶面在 levelHeight，底边和普通地面一样深（levelHeight - 块高 = -thickness），
                // 这样相邻两段的土体底部齐平，不会出现缺口
                float h = thickness + levelHeight;

                AddBlock(seg, segmentWidth * 0.5f, levelHeight - h * 0.5f,
                         segmentWidth, h, 0f, platformColor, "Ground_Top");
                break;
            }

            // ---------- 6. 空隙 ----------
            case TerrainType.Empty:
            {
                // 中间挖空，只留两端窄边 —— 和相邻段的地面连在一起，
                // 这样落点够长，不会"跳也不是、不跳也不是"
                float stub = (segmentWidth - emptyGapWidth) * 0.5f;
                if (stub > 0.05f)
                {
                    AddBlock(seg, stub * 0.5f, -thickness * 0.5f,
                             stub, thickness, 0f, flatColor, "Edge_Left");
                    AddBlock(seg, segmentWidth - stub * 0.5f, -thickness * 0.5f,
                             stub, thickness, 0f, flatColor, "Edge_Right");
                }
                break;
            }
        }

        // ---- 补土：盖住段与段接缝处漏出来的空隙 ----
        // 斜坡块是旋转的，和相邻平地之间总会咬合不严，露出一小块天空；
        // 在下方补一块和地面同宽的实心矩形，把它整个盖住。
        if (baseFillDepth > 0.01f)
        {
            if (type == TerrainType.FlatWithGap)
            {
                float side = (segmentWidth - gapWidth) * 0.5f;
                AddBlock(seg, side * 0.5f, -baseFillDepth * 0.5f,
                         side, baseFillDepth, 0f, flatColor, "Fill_Left");
                AddBlock(seg, segmentWidth - side * 0.5f, -baseFillDepth * 0.5f,
                         side, baseFillDepth, 0f, flatColor, "Fill_Right");
            }
            else if (type == TerrainType.Empty)
            {
                float stub = (segmentWidth - emptyGapWidth) * 0.5f;
                if (stub > 0.05f)
                {
                    AddBlock(seg, stub * 0.5f, -baseFillDepth * 0.5f,
                             stub, baseFillDepth, 0f, flatColor, "Fill_Left");
                    AddBlock(seg, segmentWidth - stub * 0.5f, -baseFillDepth * 0.5f,
                             stub, baseFillDepth, 0f, flatColor, "Fill_Right");
                }
            }
            else
            {
                float top = (type == TerrainType.DownSlope) ? -levelHeight : 0f;
                AddBlock(seg, segmentWidth * 0.5f, top - baseFillDepth * 0.5f,
                         segmentWidth, baseFillDepth, 0f, flatColor, "Fill");
            }
        }
        }   // end of: if (!pitPlatform) { 地面 }

        // ---- 关掉这一类型用不到的槽 ----
        for (int i = slotCursor; i < MaxBlocks; i++)
        {
            var t = seg.transform.Find("Block_" + i);
            if (t != null && t.gameObject.activeSelf) t.gameObject.SetActive(false);
        }

        // ---- 碎片槽 ----
        PlaceCoins(seg, type, hasPlatform, platX, platTop, pitPlatform, denseOnPlatform, platWidth);
    }

    GameObject AddBlock(GameObject parent, float localX, float localY,
                        float w, float h, float rotZ, Color color, string name)
    {
        int i = slotCursor++;
        var slot = parent.transform.Find("Block_" + i);
        if (slot == null) return null;

        var go = slot.gameObject;
        go.transform.localPosition = new Vector3(localX, localY, 0f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
        go.transform.localScale = new Vector3(w, h, 1f);

        // 地面和平台统一放进 "ground" 图层
        go.layer = GroundLayer;

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = color;

        if (!go.activeSelf) go.SetActive(true);

        return go;
    }

    /// <summary>ground 图层的索引（缓存，-1 表示工程里还没有这个图层）</summary>
    public static int GroundLayer
    {
        get
        {
            if (cachedGroundLayer == int.MinValue)
                cachedGroundLayer = LayerMask.NameToLayer(GroundLayerName);

            return cachedGroundLayer < 0 ? 0 : cachedGroundLayer;
        }
    }

    private static int cachedGroundLayer = int.MinValue;
    public const string GroundLayerName = "ground";

    /// <summary>主控角色宽度（世界单位）</summary>
    public float PlayerWidth()
    {
        var p = FindAnyObjectByType<ParkourPlayer>();
        if (p == null) return 1f;

        var sr = p.GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return 1f;

        return sr.sprite.bounds.size.x * p.transform.lossyScale.x;
    }

    /// <summary>允许的最小地面/平台长度（角色宽度 × X）</summary>
    public float MinPieceLength()
    {
        if (cachedPlayerWidth <= 0f) cachedPlayerWidth = PlayerWidth();
        return cachedPlayerWidth * Mathf.Max(0f, minLengthInCharacterWidths);
    }

    private float cachedPlayerWidth = -1f;

    /// <summary>该类型地形在本地 x 处的"地面高度"</summary>
    float SurfaceYAt(TerrainType type, float localX)
    {
        switch (type)
        {
            case TerrainType.Flat:
            case TerrainType.FlatWithGap:
                return 0f;

            case TerrainType.UpSlope:
                if (localX <= slopeRun) return 0f;
                if (localX >= slopeRun * 2f) return levelHeight;
                return Mathf.Lerp(0f, levelHeight, (localX - slopeRun) / slopeRun);

            case TerrainType.DownSlope:
                if (localX <= slopeRun) return 0f;
                if (localX >= slopeRun * 2f) return -levelHeight;
                return Mathf.Lerp(0f, -levelHeight, (localX - slopeRun) / slopeRun);

            case TerrainType.Platform:
                return levelHeight;

            default:
                return 0f;
        }
    }

    /// <summary>
    /// 摆放这一段的月亮碎片。
    ///
    /// 规则：
    ///   · 所有碎片的离地高度固定为 coinHeight（相对它正下方的 ground 层）
    ///   · 同一排碎片之间的横向间距固定为 coinSpacing
    ///   · 碎片可以不连续 —— 落在坑里的那些直接跳过，形成空档
    ///   · 有浮空平台时，随机让「平台」或「地面」其中一边放得密集
    /// </summary>
    void PlaceCoins(GameObject seg, TerrainType type, bool hasPlatform,
                    float platX, float platTop, bool pitPlatform,
                    bool denseOnPlatform, float platWidth)
    {
        bool allowGround = type != TerrainType.Empty;

        int total = Mathf.Clamp(hasPlatform ? coinsPerFloatingSegment : coinsPerSegment, 0, MaxCoins);

        // 平台侧 / 地面侧各放几颗
        int platformCoins = 0;
        int groundCoins;

        if (!hasPlatform)
        {
            groundCoins = total;
        }
        else if (pitPlatform)
        {
            platformCoins = total;      // 地面没了，只能全放平台
            groundCoins = 0;
        }
        else if (denseOnPlatform)
        {
            platformCoins = Mathf.Min(denseCoinCount, total);
            groundCoins = total - platformCoins;
        }
        else
        {
            groundCoins = Mathf.Min(denseCoinCount, total);
            platformCoins = total - groundCoins;
        }

        if (!allowGround) { platformCoins = 0; groundCoins = 0; }

        float spacing = Mathf.Max(0.15f, coinSpacing);
        float groundStartX = segmentWidth * 0.18f;
        float gapSideX = (segmentWidth - gapWidth) * 0.5f;

        for (int i = 0; i < MaxCoins; i++)
        {
            var t = seg.transform.Find("Coin_" + i);
            if (t == null) continue;

            bool want = spawnCoins && coinSprite != null && i < total
                        && (i < platformCoins || (i - platformCoins) < groundCoins);

            float x = 0f;
            float y = 0f;

            if (want && i < platformCoins)
            {
                // 平台上的碎片：居中排队，间距固定
                float offset = (i - (platformCoins - 1) * 0.5f) * spacing;
                float maxOffset = Mathf.Max(0f, platWidth * 0.5f - coinSize * 0.6f);
                offset = Mathf.Clamp(offset, -maxOffset, maxOffset);

                x = platX + offset;
                y = platTop + coinHeight;
            }
            else if (want)
            {
                // 地面上的碎片：从段左侧起按固定间距往右排
                int gi = i - platformCoins;
                x = groundStartX + gi * spacing;

                // 超出这一段就不放了 —— 保持间距恒定，宁可留空档
                if (x > segmentWidth - 0.4f)
                {
                    want = false;
                }

                // 落在坑里的那几颗不放（形成带空档的碎片带）
                if (type == TerrainType.FlatWithGap && x > gapSideX && x < segmentWidth - gapSideX)
                {
                    want = false;
                }
                else
                {
                    y = SurfaceYAt(type, x) + coinHeight;
                }
            }

            if (want)
            {
                t.localScale = new Vector3(coinSize, coinSize, 1f);

                // 关键：把新位置同步给碎片脚本，否则它每帧会用旧坐标把自己拉回去
                var coin = t.GetComponent<MoonCoin>();
                if (coin != null)
                {
                    coin.SetBasePosition(new Vector3(x, y, 0f));
                    coin.value = CoinValueAt(i, platformCoins, pitPlatform);
                }
                else
                {
                    t.localPosition = new Vector3(x, y, 0f);
                }

                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            }
            else if (t.gameObject.activeSelf)
            {
                t.gameObject.SetActive(false);
            }
        }
    }
}

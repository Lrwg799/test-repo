using System.Collections;
using UnityEngine;

/// <summary>
/// 吞噬后自动刷新一个新 NPC，形成「蓄力 → 吞噬 → 刷新 → 再吞噬」的循环。
///
/// 横向版本：新 NPC 刷在玩家的【右侧】（也就是玩家在它左侧 = 玩家在它身后）。
///
/// 挂在玩家（或任意管理器）上。
/// 需要一个「模板」：场景里放一份**禁用状态**的 NPC 当模板，运行时克隆它并启用，
/// 这样模板自己永远不会被吞掉。
/// </summary>
public class NPCRespawner : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("留空会自动找场景里的 NPCDevourer")]
    public NPCDevourer devourer;

    [Tooltip("NPC 模板：场景里一份【未激活】的 NPC（带 NPCDollower + LookBackWatcher）")]
    public GameObject npcTemplate;

    [Header("刷新设定")]
    [Tooltip("吞噬后等多久再刷新")]
    public float respawnDelay = 0.6f;

    [Tooltip("新 NPC 刷在玩家左右方多远。要 <= 吞噬半径，才能立刻继续吞")]
    public float spawnDistance = 2f;

    [Tooltip("在玩家左右之间随机；不勾则固定刷右侧（玩家在它身后）")]
    public bool randomSide = true;

    [Tooltip("跟随队列上限。0 = 不限")]
    public int maxFollowers = 0;

    [Header("出现动画")]
    [Tooltip("从 0 放大到原尺寸需要多久")]
    public float popTime = 0.35f;

    [Tooltip("放大过程中的过冲倍数（1.15 = 冲到 115% 再回落）")]
    public float overshoot = 1.15f;

    [Header("从右侧登场")]
    [Tooltip("从屏幕外滑入到完整露出，用多久")]
    public float revealTime = 1.2f;

    [Tooltip("滑到离屏幕右边缘多远停住（要 > 半个立绘宽，才能完整露出）")]
    public float revealInset = 1.2f;

    [Tooltip("然后玩家带镜头右移，让 NPC 回到前方，用多久")]
    public float moveTime = 0.8f;

    [Tooltip("NPC 到位后，多久之内不会自动回头（喘息时间）")]
    public float settleGraceTime = 1.5f;

    [Tooltip("地面根物体（留空会自动找场景里叫 Ground 的），推进镜头时一起移动")]
    public Transform groundRoot;

    [Tooltip("蓄力控制器（留空会自动找）。运镜期间会禁止蓄力")]
    public ChargeController charge;

    /// <summary>运镜进行中（外部可读）</summary>
    public bool IsCinematic { get; private set; }

    [Tooltip("跟随幽灵的可选贴图（会从里面随机挑一张）。留空则用玩家贴图")]
    public Sprite[] ghostSprites;

    private int spawned;

    void Awake()
    {
        if (devourer == null) devourer = GetComponent<NPCDevourer>();
        if (devourer == null) devourer = FindAnyObjectByType<NPCDevourer>();

        if (devourer != null)
        {
            devourer.onDevoured.AddListener(OnDevoured);
        }
        else
        {
            Debug.LogWarning("[Respawner] 没找到 NPCDevourer，不会自动刷新");
        }

        SyncTemplateSettings();

        if (charge == null) charge = FindAnyObjectByType<ChargeController>();

        // 把幽灵变体池交给跟随者脚本（每只随机挑一张）
        if (ghostSprites != null && ghostSprites.Length > 0)
            NPCDollower.GhostSprites = ghostSprites;
    }

    void Start()
    {
        // 换场景后先清掉场景里可能残留的跟随者（重开本关时尤其容易留下）
        foreach (var old in FindObjectsByType<NPCDollower>(FindObjectsInactive.Exclude))
        {
            if (old.isFollower) Destroy(old.gameObject);
        }

        // 换场景后清空跟随者注册表
        NPCDollower.ClearFollowers();

        // 之前夜晚已经吞掉的幽灵，这一夜继续跟着玩家
        RestorePreviousFollowers();
    }

    /// <summary>
    /// 把之前夜里吞掉的幽灵重新生成出来，让它们继续跟随。
    ///
    /// 白天的跑酷场景里没有它们（那边根本没有 NPC），
    /// 所以一回到夜晚就会重新出现。
    /// </summary>
    void RestorePreviousFollowers()
    {
        int want = Mathf.Max(0, DevourProgress.TotalDevoured);
        if (want == 0 || npcTemplate == null || devourer == null) return;

        // 显示上限：吞得再多也只显示这么多只，避免整队拉出屏幕
        if (devourer.followerCap > 0) want = Mathf.Min(want, devourer.followerCap);

        Transform anchor = devourer.playerRoot != null ? devourer.playerRoot : transform;

        Sprite ghostSprite = devourer.playerVisual != null ? devourer.playerVisual.sprite : null;
        Vector3 ghostScale = devourer.playerVisual != null
            ? devourer.playerVisual.transform.lossyScale
            : Vector3.one;

        // 先算好落点表，再生成 —— 这样每只一出生就在自己的位置上
        NPCDollower templateFollower = npcTemplate.GetComponent<NPCDollower>();
        if (templateFollower != null) templateFollower.RebuildSlots(want);

        for (int i = 0; i < want; i++)
        {
            // 先摆到玩家身后一点，避免叠在玩家身上
            Vector3 pos = anchor.position + new Vector3(-1.5f - i * 0.3f, 0f, 0f);

            GameObject npc = Instantiate(npcTemplate, pos, Quaternion.identity);
            npc.name = "Ghost_" + i;
            npc.SetActive(true);

            var follower = npc.GetComponent<NPCDollower>();
            if (follower == null)
            {
                Destroy(npc);
                continue;
            }

            follower.queueIndex = i;
            follower.devouredSprite = ghostSprite;
            follower.matchPlayerScale = true;
            follower.playerScale = ghostScale;

            follower.BeginFollow(anchor);
            follower.RevealImmediately();     // 恢复的幽灵不用等刷新，直接浮现
        }

        Debug.Log("[Respawner] 恢复了 " + want + " 只之前吞掉的幽灵，继续跟随玩家");
    }

    /// <summary>
    /// 让模板的「难度参数」跟随场上那只 NPC。
    /// 否则改了一个、忘了改另一个，刷新出来的 NPC 行为就会和场上不一致
    /// （比如 suddenChance 改了 0，模板还是 0.5 → 新 NPC 仍会"不预警直接回头"）。
    /// </summary>
    void SyncTemplateSettings()
    {
        if (npcTemplate == null) return;

        // 模板上残留的 playerScale 可能是旧的（换过玩家贴图/缩放之后就对不上了），
        // 这里统一刷成玩家当前的实际缩放，保证跟随者不会莫名变大一倍
        if (devourer != null && devourer.playerVisual != null)
        {
            var tf = npcTemplate.GetComponent<NPCDollower>();
            if (tf != null)
            {
                tf.matchPlayerScale = true;
                tf.playerScale = devourer.playerVisual.transform.lossyScale;
            }
        }

        var template = npcTemplate.GetComponent<LookBackWatcher>();
        if (template == null) return;

        // 模板是未激活的，所以这里只会拿到场上活跃的那只
        var source = FindAnyObjectByType<LookBackWatcher>();
        if (source == null || source == template) return;

        template.suddenChance = source.suddenChance;
        template.bluffChance = source.bluffChance;
        template.warnDuration = source.warnDuration;
        template.autoInterval = source.autoInterval;
        template.intervalJitter = source.intervalJitter;
        template.randomizeStartTimer = source.randomizeStartTimer;
        template.graceTime = source.graceTime;
        template.watchDuration = source.watchDuration;
        template.senseRadius = source.senseRadius;

        Debug.Log("[Respawner] 模板难度参数已同步自「" + source.gameObject.name + "」"
                + "（sudden=" + template.suddenChance + " bluff=" + template.bluffChance + "）");
    }

    void OnDestroy()
    {
        if (devourer != null)
        {
            devourer.onDevoured.RemoveListener(OnDevoured);
        }
    }

    void OnDevoured()
    {
        if (maxFollowers > 0 && devourer != null && devourer.FollowerCount >= maxFollowers)
        {
            Debug.Log("[Respawner] 队列已达上限 " + maxFollowers + "，不再刷新");
            return;
        }

        if (npcTemplate == null)
        {
            Debug.LogWarning("[Respawner] 没设置 npcTemplate，无法刷新");
            return;
        }

        StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        // 运镜期间禁止蓄力：NPC 还在屏幕外，蓄满也吞不到，等于白蓄
        IsCinematic = true;
        if (charge != null)
        {
            charge.CancelCharge();
            charge.interactable = false;
        }

        yield return new WaitForSeconds(respawnDelay);

        Transform anchor = devourer != null && devourer.playerRoot != null
            ? devourer.playerRoot
            : transform;

        // 生成在屏幕右外侧
        Camera camRef = Camera.main;
        float halfW = camRef != null ? camRef.orthographicSize * camRef.aspect : 8.9f;
        float camRefX = camRef != null ? camRef.transform.position.x : 0f;

        Vector3 spawnPos = new Vector3(camRefX + halfW + 1.2f, anchor.position.y, 0f);

        GameObject npc = Instantiate(npcTemplate, spawnPos, Quaternion.identity);
        spawned++;
        npc.name = "NPC_" + spawned;
        npc.SetActive(true);

        // 关键：入场运镜期间冻结它的警觉逻辑
        // 否则它会在屏幕外就开始计时，玩家还没走到它身后就回头了
        var newWatcher = npc.GetComponent<LookBackWatcher>();
        if (newWatcher != null)
        {
            newWatcher.Cancel();
            newWatcher.enabled = false;
        }

        // 出现动画：0 → 过冲 → 1
        Vector3 finalScale = npcTemplate.transform.localScale;
        npc.transform.localScale = Vector3.zero;

        float t = 0f;
        while (t < popTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / popTime);

            float s;
            if (k < 0.75f)
            {
                s = (k / 0.75f) * overshoot;          // 快速放大并过冲
            }
            else
            {
                s = Mathf.Lerp(overshoot, 1f, (k - 0.75f) / 0.25f);   // 回落到 1
            }

            npc.transform.localScale = finalScale * s;
            yield return null;
        }

        npc.transform.localScale = finalScale;

        // ---- 2) 从屏幕右外侧缓缓滑入，直到完整露出立绘 ----
        Camera cam = Camera.main;
        float halfWidth = cam != null
            ? cam.orthographicSize * cam.aspect
            : 8.9f;

        float camX = cam != null ? cam.transform.position.x : 0f;

        float startX = camX + halfWidth + 1.2f;                 // 完全在屏幕外
        float edgeX = camX + halfWidth - revealInset;           // 刚好完整露出

        if (revealTime > 0.01f)
        {
            float r = 0f;
            while (r < revealTime && npc != null)
            {
                r += Time.deltaTime;
                float k = Mathf.Clamp01(r / revealTime);
                k = k * k * (3f - 2f * k);

                var pos = npc.transform.position;
                pos.x = Mathf.Lerp(startX, edgeX, k);
                npc.transform.position = pos;
                yield return null;
            }
        }

        if (npc != null)
        {
            var pos = npc.transform.position;
            pos.x = edgeX;
            npc.transform.position = pos;
        }

        // ---- 3) 玩家 + 跟随者 + 镜头一起右移，让 NPC 回到玩家前方 ----
        // 移动量 = 把 NPC 从"右边缘"拉回"距玩家 spawnDistance"
        float npcX = npc != null ? npc.transform.position.x : anchor.position.x + spawnDistance;
        float wantPlayerX = npcX - spawnDistance;
        float deltaX = wantPlayerX - anchor.position.x;

        Vector3 playerStart = anchor.position;
        Vector3 camStart = cam != null ? cam.transform.position : Vector3.zero;

        // 地面跟着一起走，这样无论推进多少次都不会看到地图边缘
        Transform ground = FindGroundRoot();
        Vector3 groundStart = ground != null ? ground.position : Vector3.zero;

        if (moveTime > 0.01f && Mathf.Abs(deltaX) > 0.01f)
        {
            float moved = 0f;                                    // 已经移动的距离
            float speed = Mathf.Abs(deltaX) / moveTime;          // 匀速

            while (moved < Mathf.Abs(deltaX))
            {
                moved += speed * Time.deltaTime;
                if (moved > Mathf.Abs(deltaX)) moved = Mathf.Abs(deltaX);

                float k = moved / Mathf.Abs(deltaX);             // 线性，没有任何加速/减速

                anchor.position = playerStart + new Vector3(deltaX * k, 0f, 0f);
                if (cam != null)
                    cam.transform.position = camStart + new Vector3(deltaX * k, 0f, 0f);
                if (ground != null)
                    ground.position = groundStart + new Vector3(deltaX * k, 0f, 0f);

                yield return null;
            }
        }

        anchor.position = playerStart + new Vector3(deltaX, 0f, 0f);
        if (cam != null) cam.transform.position = camStart + new Vector3(deltaX, 0f, 0f);
        if (ground != null) ground.position = groundStart + new Vector3(deltaX, 0f, 0f);

        // 玩家已经站到 NPC 身后，开放蓄力
        IsCinematic = false;
        if (charge != null) charge.interactable = true;

        // 同时放开它的警觉：先给一段喘息，别一到位就回头
        if (newWatcher != null)
        {
            newWatcher.Cancel();
            newWatcher.enabled = true;
            newWatcher.GiveGrace(settleGraceTime);
        }

        Debug.Log("[Respawner] " + npc.name + " 从右侧登场，镜头匀速推进 "
                  + deltaX.ToString("0.00") + " 单位，回到开局构图（可以蓄力了）");
    }

    /// <summary>找到地面根物体（名字叫 Ground），推进镜头时一起移动，避免露出地图边缘</summary>
    Transform FindGroundRoot()
    {
        if (groundRoot != null) return groundRoot;

        var go = GameObject.Find("Ground");
        if (go != null) groundRoot = go.transform;
        return groundRoot;
    }
}

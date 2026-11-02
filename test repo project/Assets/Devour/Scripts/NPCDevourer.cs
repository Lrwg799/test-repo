using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 吞噬机制：蓄力成功（ChargeController.onChargeFired）的那一刻，
/// 把 devourRadius 范围内最近的 NPC 吞掉，让它变成跟随者，排在队列里跟在玩家身后。
///
/// 队列规则：第 1 个被吞的贴在玩家身后最近，第 2 个再往后一个 queueSpacing，
/// 以此类推。玩家转身时整列会一起换到另一侧。
///
/// 挂在【玩家】身上。
/// </summary>
public class NPCDevourer : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("留空会自动找本物体 / 场景里的 ChargeController")]
    public ChargeController charge;

    [Tooltip("玩家根节点，留空 = 本物体")]
    public Transform playerRoot;

    [Tooltip("玩家的显示用 SpriteRenderer：吞掉 NPC 后把它的贴图/缩放复制给 NPC。\n留空 = 用本物体上的 SpriteRenderer")]
    public SpriteRenderer playerVisual;

    [Header("吞噬设定")]
    [Tooltip("吞噬半径（世界单位）")]
    public float devourRadius = 3f;

    [Tooltip("跟随队列里相邻两行的间距")]
    public float queueSpacing = 0.9f;

    [Tooltip("队列每行最多几个跟随者，超过就换行")]
    public int maxPerRow = 3;

    [Tooltip("同一行内上下错开的间距")]
    public float rowGap = 1.7f;

    [Tooltip("吞噬后是否把 NPC 的缩放也改成和玩家一样")]
    public bool matchPlayerScale = true;

    [Tooltip("跟随幽灵的显示上限。超过这个数的照样算吞噬，但不再生成幽灵（0 = 不限）")]
    public int followerCap = 12;

    [Header("事件")]
    [Tooltip("成功吞噬一个 NPC 之后触发（用来刷新新 NPC、播放特效等）")]
    public UnityEvent onDevoured;

    // 已吞噬的跟随者（按吞噬顺序）
    private readonly List<NPCDollower> followers = new List<NPCDollower>();

    /// <summary>当前队列长度</summary>
    public int FollowerCount { get { return followers.Count; } }

    void Awake()
    {
        if (playerRoot == null) playerRoot = transform;

        // 防错：玩家立绘如果被拖离根节点，把偏移烘焙回根节点，保证逻辑与画面一致
        if (playerVisual != null && playerVisual.transform.parent == transform)
        {
            Vector3 off = playerVisual.transform.position - transform.position;
            if (off.sqrMagnitude > 0.0001f)
            {
                transform.position += off;
                playerVisual.transform.localPosition = Vector3.zero;
            }
        }

        if (charge == null) charge = GetComponent<ChargeController>();
        if (charge == null) charge = FindAnyObjectByType<ChargeController>();

        if (charge != null)
        {
            charge.onChargeFired.AddListener(OnChargeFired);
        }
        else
        {
            Debug.LogWarning("[Devourer] 没找到 ChargeController，吞噬不会触发");
        }
    }

    void OnDestroy()
    {
        if (charge != null)
        {
            charge.onChargeFired.RemoveListener(OnChargeFired);
        }
    }

    void OnChargeFired()
    {
        // 运镜期间（新 NPC 还在屏幕外/玩家还没走到它身后）一律不许吞
        var respawner = FindAnyObjectByType<NPCRespawner>();
        if (respawner != null && respawner.IsCinematic)
        {
            Debug.Log("[Devourer] NPC 还没就位，这次吞噬无效");
            return;
        }

        // 先清掉已经被销毁的跟随者，保证队列连续
        for (int i = followers.Count - 1; i >= 0; i--)
        {
            if (followers[i] == null) followers.RemoveAt(i);
        }

        // 找范围内最近的、还没被吞的 NPC
        NPCDollower nearest = null;
        float best = devourRadius;

        NPCDollower[] all = FindObjectsByType<NPCDollower>(FindObjectsInactive.Exclude);

        for (int i = 0; i < all.Length; i++)
        {
            NPCDollower npc = all[i];
            if (npc == null || npc.isFollower) continue;

            // 正在警戒（亮警告标志 / 回头盯着你）的 NPC 吞不了 ——
            // 否则玩家可以在它预警时把它吃掉，把危险直接取消掉
            var watcher = npc.GetComponent<LookBackWatcher>();
            if (watcher != null && (watcher.IsWatching || watcher.IsWarning)) continue;

            float d = Vector2.Distance(transform.position, npc.transform.position);
            if (d <= best)
            {
                best = d;
                nearest = npc;
            }
        }

        if (nearest == null)
        {
            Debug.Log("[Devourer] 蓄力完成，但 " + devourRadius + " 范围内没有可吞噬的 NPC"
                      + "（可能它正在警戒）");
            return;
        }

        // ---- 解析「玩家形象」----
        SpriteRenderer src = playerVisual != null
            ? playerVisual
            : GetComponent<SpriteRenderer>();

        Sprite playerSprite = null;
        Vector3 playerScale = Vector3.one;

        if (src != null)
        {
            playerSprite = src.sprite;
            // 用世界缩放：NPC 通常是根物体，localScale 会和玩家实际渲染大小对不上
            playerScale = src.transform.lossyScale;
        }

        if (playerSprite == null)
        {
            Debug.LogWarning("[Devourer] 没拿到玩家贴图（playerVisual 为空或它的 sprite 为空），NPC 会保持原样");
        }

        // ---- 跟随队列满了：这一只直接消失（不占用显示名额）----
        // 用场上总人数判断，不能用本局吞掉的数量（上一夜恢复的不在这个列表里）
        if (followerCap > 0 && NPCDollower.TotalFollowers >= followerCap)
        {
            if (onDevoured != null) onDevoured.Invoke();

            Debug.Log("[Devourer] 吞噬了 " + nearest.name
                      + "（距离 " + best.ToString("0.00") + "），队列已满 " + followerCap + "，这只不再显示");

            Destroy(nearest.gameObject);
            return;
        }

        // ---- 把队列信息和形象交给 NPC ----
        nearest.queueIndex = followers.Count;
        nearest.queueSpacing = queueSpacing;
        nearest.maxPerRow = maxPerRow;
        nearest.rowGap = rowGap;
        nearest.devouredSprite = playerSprite;
        nearest.matchPlayerScale = matchPlayerScale;
        nearest.playerScale = playerScale;

        nearest.BeginFollow(playerRoot);

        followers.Add(nearest);

        // 落点表由 BeginFollow 内部按「场上总人数」重排，这里不用再算

        if (onDevoured != null) onDevoured.Invoke();

        Debug.Log("[Devourer] 吞噬了 " + nearest.name
                  + "（距离 " + best.ToString("0.00") + "），队列长度 " + followers.Count);
    }
}

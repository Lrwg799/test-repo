using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD：金币 / 死亡 / 计时 / 操作提示 / 通关面板。
/// 使用 uGUI 的 Text + 系统动态字体（支持中文），无需额外字体资源。
/// </summary>
public class HUDController : MonoBehaviour
{
    public Text coinText;
    public Text deathText;
    public Text timeText;
    public Text hintText;
    public CanvasGroup hintGroup;
    public GameObject winPanel;
    public Text winTitle;
    public Text winStats;

    static Font cachedFont;
    float hintTimer = 12f;

    void Awake()
    {
        Font font = ResolveFont();
        if (font != null)
        {
            Text[] texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++) texts[i].font = font;
        }

        if (winPanel != null) winPanel.SetActive(false);
    }

    static Font ResolveFont()
    {
        if (cachedFont != null) return cachedFont;

        string[] candidates = { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Segoe UI", "Arial" };
        for (int i = 0; i < candidates.Length; i++)
        {
            try
            {
                Font f = Font.CreateDynamicFontFromOSFont(candidates[i], 40);
                if (f != null)
                {
                    cachedFont = f;
                    return cachedFont;
                }
            }
            catch { }
        }

        cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return cachedFont;
    }

    void Update()
    {
        if (hintTimer > 0f)
        {
            hintTimer -= Time.unscaledDeltaTime;
            if (hintTimer <= 0f && hintGroup != null) hintGroup.alpha = 0f;
        }
    }

    public void SetCoins(int coins, int total)
    {
        if (coinText != null) coinText.text = coins + " / " + total;
    }

    public void SetDeaths(int deaths)
    {
        if (deathText != null) deathText.text = "死亡 " + deaths;
    }

    public void SetTime(float seconds)
    {
        if (timeText != null) timeText.text = FormatTime(seconds);
    }

    public void ShowWin(int coins, int total, int deaths, float time)
    {
        if (hintGroup != null) hintGroup.alpha = 0f;
        if (winPanel != null) winPanel.SetActive(true);
        if (winTitle != null) winTitle.text = "关卡完成！";
        if (winStats != null)
            winStats.text = "金币  " + coins + " / " + total + "\n死亡  " + deaths + "\n用时  " + FormatTime(time) + "\n\n按 R 重新挑战";
    }

    public static string FormatTime(float seconds)
    {
        int minutes = (int)(seconds / 60f);
        int secs = (int)(seconds % 60f);
        return string.Format("{0:00}:{1:00}", minutes, secs);
    }
}

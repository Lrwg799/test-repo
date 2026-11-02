using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 关卡流程：金币、死亡、检查点、计时、通关、重开。
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public PlayerController2D player;
    public PlatformerCamera gameCamera;
    public HUDController hud;
    public int totalCoins = 0;
    public Vector3 spawnPoint;
    public float killY = -9f;

    public bool LevelComplete { get; private set; }
    public int Coins { get; private set; }
    public int Deaths { get; private set; }
    public float Elapsed { get; private set; }

    void Awake()
    {
        Instance = this;
        if (player != null) spawnPoint = player.transform.position;
    }

    void Start()
    {
        if (hud != null)
        {
            hud.SetCoins(Coins, totalCoins);
            hud.SetDeaths(Deaths);
            hud.SetTime(Elapsed);
        }
    }

    void Update()
    {
        if (GameInput.RestartPressed)
        {
            RestartLevel();
            return;
        }

        if (LevelComplete) return;

        Elapsed += Time.deltaTime;
        if (hud != null) hud.SetTime(Elapsed);

        if (player != null && !player.IsDead && player.transform.position.y < killY)
            player.Die();
    }

    public void AddCoin(int amount)
    {
        if (LevelComplete) return;
        Coins += amount;
        if (hud != null) hud.SetCoins(Coins, totalCoins);
    }

    public void SetCheckpoint(Vector3 position)
    {
        spawnPoint = position;
    }

    public void RespawnPlayer()
    {
        Deaths++;
        if (hud != null) hud.SetDeaths(Deaths);
        if (player != null) player.Respawn(spawnPoint);
        if (gameCamera != null) gameCamera.SnapToTarget();
    }

    public void ShakeCamera(float magnitude, float duration)
    {
        if (gameCamera != null) gameCamera.Shake(magnitude, duration);
    }

    public void CompleteLevel()
    {
        if (LevelComplete) return;
        LevelComplete = true;
        if (player != null) player.LockInput(true);
        if (hud != null) hud.ShowWin(Coins, totalCoins, Deaths, Elapsed);
        SfxPlayer.Play(SfxPlayer.Win, 0.9f);
        ShakeCamera(0.12f, 0.3f);
    }

    public void RestartLevel()
    {
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name);
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance { get; private set; }

    [SerializeField] private GameOverUI gameOverUI;   // assign in Inspector
    [SerializeField] private bool pauseOnGameOver = true;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // Keep if you have multi-scene flow; otherwise you can remove this:
        DontDestroyOnLoad(gameObject);
    }

    public void TriggerGameOver(string reason = "")
    {
        int score = ScoreManager.Instance ? ScoreManager.Instance.Score : 0;
        if (gameOverUI) gameOverUI.Show(score, reason);
        if (pauseOnGameOver) Time.timeScale = 0f;
    }

    // Called by UI button
    public void Restart()
    {
        Time.timeScale = 1f;
        if (ScoreManager.Instance) ScoreManager.Instance.ResetScore(0);
        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }
}

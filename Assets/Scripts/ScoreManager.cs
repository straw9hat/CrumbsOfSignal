using System;
using UnityEngine;

[DefaultExecutionOrder(-1000)] // init before most things
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    public static event Action<int> OnScoreChangedStatic; // UI can listen even if Instance swaps

    public int Score { get; private set; }

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        // fire once so late UIs get an initial value
        OnScoreChangedStatic?.Invoke(Score);
    }

    public void AddPoints(int amount) => SetScore(Score + amount);
    public void ResetScore(int value = 0) => SetScore(value);

    private void SetScore(int newValue)
    {
        Score = Mathf.Max(0, newValue);
        OnScoreChangedStatic?.Invoke(Score);
        // Debug.Log($"[ScoreManager] Score = {Score}");
    }

    // Convenience if you ever call before Instance exists
    public static void AddPointsStatic(int amount)
    {
        if (Instance) Instance.AddPoints(amount);
        else OnScoreChangedStatic?.Invoke(Mathf.Max(0, amount)); // shows something if UI appears first
    }
}

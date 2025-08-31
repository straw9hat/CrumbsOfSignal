using TMPro;
using UnityEngine;

public class ScoreUI : MonoBehaviour
{
    [SerializeField] private TMP_Text scoreText;

    void OnEnable()
    {
        ScoreManager.OnScoreChangedStatic += Refresh;
        // pull current value (handles both cases: Instance exists or not yet)
        Refresh(ScoreManager.Instance ? ScoreManager.Instance.Score : 0);
    }
    void OnDisable()
    {
        ScoreManager.OnScoreChangedStatic -= Refresh;
    }

    private void Refresh(int value)
    {
        if (scoreText) scoreText.text = $"Score: {value}";
        // Debug.Log($"[ScoreUI] Score updated to {value}");
    }
}

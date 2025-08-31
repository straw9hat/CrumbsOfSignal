using TMPro;
using UnityEngine;

public class GameOverUI : MonoBehaviour
{
    [SerializeField] private GameObject root;          // the panel GameObject
    [SerializeField] private TMP_Text scoreText;

    public void Show(int score, string reason = "")
    {
        if (scoreText) scoreText.text = $"Score: {score}";
        if (root) root.SetActive(true);
        else gameObject.SetActive(true);
    }

    // Hook this to the Restart button OnClick:
    public void OnClickRestart()
    {
        GameOverManager.Instance?.Restart();
    }
}

using TMPro;
using UnityEngine;

public class ScoreUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private BattleRecordManager battleRecordManager;

    private void Start()
    {
        UpdateScore();
    }
    public void UpdateScore()
    {
        if (scoreText == null || battleRecordManager == null) return;

        scoreText.text = $"{battleRecordManager.Player1Wins}"
            + $" - "
            + $"{battleRecordManager.Player2Wins}";
        Debug.Log($"ScoreUI updated: {scoreText.text}");
    }
}
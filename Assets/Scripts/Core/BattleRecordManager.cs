using UnityEngine;

public class BattleRecordManager : MonoBehaviour
{
    public int Player1Wins { get; private set; }
    public int Player2Wins { get; private set; }
    public int Draws { get; private set; }

    public void AddPlayerWin(FlipCheck winner)
    {
        if (winner == null) return;

        if (winner.gameObject.name.Contains("Player1"))
        {
            Player1Wins++;
        }
        else
        {
            Player2Wins++;
        }
    }
    public void AddPlayer2Win() => Player2Wins++;
    public void AddDraw() => Draws++;
    public void ResetRecords()
    {
        Player1Wins = 0;
        Player2Wins = 0;
        Draws = 0;
    }

    public int GetTotalBattles()
    {
        return Player1Wins + Player2Wins + Draws;
    }

    // Get Player's score 

}

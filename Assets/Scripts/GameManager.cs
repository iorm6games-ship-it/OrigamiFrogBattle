using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static int currentRound = 1;
    public enum GameState
    {
        Waiting,
        Ready,
        Go,
        Playing,
        GameOver
    }
    public static GameState currentState = GameState.Ready;

    public static void NextRound()
    {
        currentRound++;
    }

}

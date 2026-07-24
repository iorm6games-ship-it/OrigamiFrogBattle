using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleManager : MonoBehaviour
{
    [SerializeField] private string mainSceneName = "MainScene";

    public void StartGame()
    {
        mainSceneName = "SampleScene"; // ここでシーン名を指定
        SceneManager.LoadScene(mainSceneName);
    }

    public void ExitGame()
    {
        Application.Quit();
    }
}
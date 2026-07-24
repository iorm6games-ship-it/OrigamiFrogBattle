using UnityEngine;

public class ScreenshotCapture : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            string path = Application.dataPath + "/../title_portrait.png";
            ScreenCapture.CaptureScreenshot(path, 2);
            Debug.Log("Saved screenshot: " + path);
        }
    }
}
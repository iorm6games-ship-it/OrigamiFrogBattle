using UnityEngine;
using TMPro;
using System.Collections;

public class GameJudge: MonoBehaviour
{
	[SerializeField] private TMP_Text resultText;
	[SerializeField] private CameraController cameraController;

	[Header("Result Text Style")]
	[SerializeField] private TMP_FontAsset resultFont;
	[SerializeField] private Color resultTextColor = Color.white;
	[SerializeField] private int resultFontSize = 80;
	[SerializeField] private float resultTextAlpha = 1f;
	[SerializeField] private bool useOutline = true;
	[SerializeField] private Color outlineColor = Color.black;
	[SerializeField] private float outlineWidth = 0.2f;

	[SerializeField] private float resultTextDelay = 2.2f;

    private void ShowResult(string result)
	{
		if (resultText != null)
		{
			resultText.gameObject.SetActive(true);

			// Set font if available
			if (resultFont != null)
			{
				resultText.font = resultFont;
			}

			// Set color and alpha
			Color displayColor = new Color(resultTextColor.r, resultTextColor.g, resultTextColor.b, resultTextAlpha);
			resultText.color = displayColor;

			// Set font size
			resultText.fontSize = resultFontSize;

			// Set outline if enabled
			if (useOutline)
			{
				resultText.outlineWidth = outlineWidth;
				resultText.outlineColor = outlineColor;
			}
			else
			{
				resultText.outlineWidth = 0f;
			}

			resultText.text = result;
		}
		else
		{
			Debug.Log(result);
		}
	}

	private FlipCheck[] frogs;

	private bool judged = false;
	private string winResult = "";
	private FlipCheck currentWinner = null;

	private IEnumerator ShowResultDelayed(string result)
	{
		if (resultText != null)
		{
			resultText.text = "";
			resultText.gameObject.SetActive(false);
		}
		yield return new WaitForSeconds(resultTextDelay);

		ShowResult(result);
	}

	void Start()
	{
		frogs = FindObjectsByType<FlipCheck>();
		if (resultText != null)
		{
			resultText.text = "";
		}
	}
	// Update is called once per frame
	void Update()
	{

		if (GameManager.currentState == GameManager.GameState.GameOver) 
		{
			return;
		}
		if (judged)
		{
			return;
		}
		if (frogs == null || frogs.Length == 0)
		{
			return;
		}

		// Check if all frogs have stopped
		bool allStopped = true;
		foreach (FlipCheck frog in frogs)
		{
			if (!frog.HasStopped)
			{
				allStopped = false;
				break;
			}
		}

		if (!allStopped)
		{
			return;
		}

		bool hasPlayerContact = false;

		foreach (FlipCheck frog in frogs)
		{
			if (frog.HasPlayerContacted)
			{
				hasPlayerContact = true;
				break;
			}
		}

		string result = "";
		if (!hasPlayerContact)
		{
			judged = true;
			result = "No Contest ( Not contacted )";
			GameManager.currentState = GameManager.GameState.GameOver;
			ShowResult(result);
			return;
		}
        int backDownCount = 0;
        FlipCheck loser = null;
        FlipCheck winner = null;

        foreach (FlipCheck frog in frogs)
        {
            if (frog.IsBackDown())
            {
                backDownCount++;
                loser = frog;

            }
        }
        
		if (backDownCount == 1)
		{
            if (loser != null)
            {
                foreach (FlipCheck frog in frogs)
                {
                    if (frog != loser)
                    {
                        winner = frog;
                        break;
                    }
                }

                result = winner != null
                    ? $"{winner.gameObject.name} WIN!!"
                    : "DRAW !";
            }
        }
		else
		{
			result = "DRAW !";
			ShowResult(result);
			GameManager.currentState = GameManager.GameState.GameOver;
            return;
        }
		
        judged = true;
		GameManager.currentState = GameManager.GameState.GameOver;

		if (winner != null && loser != null && cameraController != null)
		{
			if (resultText != null)
			{
				resultText.text = "";
				resultText.gameObject.SetActive(false);
			}

			// Store winner result and set callback for display
			winResult = result;
			currentWinner = winner;

			// Set callback to show result when Winner is focused
			cameraController.SetOnWinnerFocusedCallback(() =>
			{
				ShowResult(winResult);
			});

			// PlayResultSequence expects (loser, winner)
			cameraController.PlayResultSequence(
				loser.transform,
				winner.transform
			);
		}
		else
		{
			ShowResult(result);
		}
	}
	
	public void ResetJudge()
	{
		judged = false;
		frogs = FindObjectsByType<FlipCheck>();
		
		if (resultText != null)
		{
			resultText.text = "";
			
		}
	}
}

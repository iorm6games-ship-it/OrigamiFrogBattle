using UnityEngine;
using System.Collections;
using TMPro;

public class StartCountdown: MonoBehaviour
{
	[SerializeField] private TextMeshProUGUI startText;
	[SerializeField] private AudioSource audioSource;
	[SerializeField] private AudioClip readySE;

	[Header("Round Text Style")]
	[SerializeField] private TMP_FontAsset roundFont;
	[SerializeField] private Color roundColor = new Color(0f, 0f, 0f, 1f);
	[SerializeField] private int roundFontSize = 100;
	[SerializeField] private float roundDisplayDuration = 0.7f;
	[SerializeField] private float roundFadeSpeed = 2f;
	[SerializeField] private bool roundUseOutline = false;
	[SerializeField] private Color roundOutlineColor = Color.black;
	[SerializeField] private float roundOutlineWidth = 0.2f;

	[Header("Ready Text Style")]
	[SerializeField] private TMP_FontAsset readyFont;
	[SerializeField] private Color readyColor = new Color(1f, 1f, 0f, 0.7f);
	[SerializeField] private int readyFontSize = 250;
	[SerializeField] private float readyDisplayDuration = 1.5f;
	[SerializeField] private bool readyUseOutline = true;
	[SerializeField] private Color readyOutlineColor = Color.black;
	[SerializeField] private float readyOutlineWidth = 0.2f;

	[Header("Go Text Style")]
	[SerializeField] private TMP_FontAsset goFont;
	[SerializeField] private Color goColor = new Color(1f, 0f, 0f, 1f);
	[SerializeField] private int goFontSize = 350;
	[SerializeField] private float goFadeSpeed = 2f;
	[SerializeField] private bool goUseOutline = true;
	[SerializeField] private Color goOutlineColor = Color.black;
	[SerializeField] private float goOutlineWidth = 0.2f;

	public static bool IsReadyTime { get; private set; }
	public static bool IsGoTime { get; private set; }

	// Use this for initialization
	void Start()
	{
		StartCoroutine(Countdown());
	}
	public void ResetCountdown()
	{
		StopAllCoroutines();
		StartCoroutine(Countdown());
	}

	private IEnumerator Countdown()
	{
		GameManager.currentState = GameManager.GameState.Waiting;

		IsReadyTime = false;
		IsGoTime = false;

		startText.gameObject.SetActive(false);

		startText.gameObject.SetActive(true);
		if (roundFont != null) startText.font = roundFont;
		startText.color = roundColor;
		startText.fontSize = roundFontSize;
		startText.rectTransform.anchoredPosition = new Vector2(0f, 50f);
		ApplyOutline(startText, roundUseOutline, roundOutlineColor, roundOutlineWidth);
		startText.text = $"Round {GameManager.currentRound}";
		yield return new WaitForSeconds(roundDisplayDuration);

		for (float a = 1f; a > 0; a -= Time.deltaTime * roundFadeSpeed)
		{
			startText.color = new Color(roundColor.r, roundColor.g, roundColor.b, a);
			yield return null;
		}

		if (readyFont != null) startText.font = readyFont;
		startText.fontSize = readyFontSize;
		startText.color = readyColor;
		startText.rectTransform.anchoredPosition = new Vector2(0f, 50f);
		ApplyOutline(startText, readyUseOutline, readyOutlineColor, readyOutlineWidth);
		GameManager.currentState = GameManager.GameState.Ready;
		startText.text = "READY";
		audioSource.PlayOneShot(readySE);

		IsReadyTime = true;

		yield return new WaitForSeconds(readyDisplayDuration);
		if (goFont != null) startText.font = goFont;
		startText.color = goColor;
		startText.fontSize = goFontSize;
		ApplyOutline(startText, goUseOutline, goOutlineColor, goOutlineWidth);
		GameManager.currentState = GameManager.GameState.Go;
		startText.text = "GO";
		IsGoTime = true;

		for (float a = 1f; a > 0f; a -= Time.deltaTime * goFadeSpeed)
		{
			startText.color = new Color(goColor.r, goColor.g, goColor.b, a);
			yield return null;
		}

		startText.gameObject.SetActive(false);
		
	}

	private void ApplyOutline(TextMeshProUGUI text, bool useOutline, Color outlineColor, float outlineWidth)
	{
		if (useOutline)
		{
			text.outlineWidth = outlineWidth;
			text.outlineColor = outlineColor;
		}
		else
		{
			text.outlineWidth = 0f;
		}
	}
}


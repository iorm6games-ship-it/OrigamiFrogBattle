using UnityEngine;

public class FrogSound: MonoBehaviour
{

	[SerializeField] public AudioSource audioSource;
	[SerializeField] public AudioClip[] jumpClips;
	[SerializeField] public AudioClip crouchSE;

	public void PlayJump(int jumpLevel)
	{
		if (audioSource == null)
		{
			Debug.LogWarning("AudioSource is not set.");
			return;
		}
		if (jumpClips == null || jumpClips.Length == 0)
		{
			Debug.LogWarning("AudioClips are not set.");
			return;
		}
		int index = Mathf.Clamp(jumpLevel -1, 0, jumpClips.Length -1);

		if (jumpClips[index] == null)
		{
			Debug.LogWarning($"Jump clip is missing. level={jumpLevel}, index={index}");
			return;
		}
		audioSource.pitch = Random.Range(0.97f, 1.03f);
		audioSource.volume = 0.15f + index * 0.2125f;

		audioSource.PlayOneShot(jumpClips[index]);
	}
	
	public void PlayCrouch()
	{
		audioSource.PlayOneShot(crouchSE);
	}
}
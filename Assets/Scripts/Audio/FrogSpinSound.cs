using UnityEngine;

public class FrogSpinSound : MonoBehaviour
{
    [SerializeField] private Rigidbody rb;
    [SerializeField] private AudioSource spinAudioSource;

    [Header("Spin Sound")]
    [SerializeField] private float startSpinSpeed = 5f;
    [SerializeField] private float maxSpinSpeed = 20f;
    [SerializeField] private float maxVolume = 0.5f;
    [SerializeField] private float minPitch = 0.9f;
    [SerializeField] private float maxPitch = 1.25f;

    private void Start()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (spinAudioSource != null)
        {
            spinAudioSource.loop = true;
            spinAudioSource.playOnAwake = false;
        }

    }

    private void Update()
    {
        if (rb == null || spinAudioSource == null) return;

        float spinSpeed = rb.angularVelocity.magnitude;

        if (spinSpeed < startSpinSpeed || GameManager.currentState == GameManager.GameState.GameOver)
        {
            if (spinAudioSource.isPlaying)
            {
                spinAudioSource.Stop();
            }
            return;
        }

        float rate = Mathf.InverseLerp(startSpinSpeed, maxSpinSpeed, spinSpeed);

        spinAudioSource.volume = Mathf.Lerp(0.05f, maxVolume, rate);
        spinAudioSource.pitch = Mathf.Lerp(minPitch, maxPitch, rate);

        if (!spinAudioSource.isPlaying)
        {
            spinAudioSource.Play();
        }

    }
}

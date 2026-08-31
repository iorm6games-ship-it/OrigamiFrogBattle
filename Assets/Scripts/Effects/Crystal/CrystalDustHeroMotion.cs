using System;
using UnityEngine;

public sealed class CrystalDustHeroMotion: MonoBehaviour
{
    [Header("Burst")]
    [SerializeField]
    private float burstDuration = 0.35f;

    [SerializeField]
    private float burstDistance = 0.8f;

    [Header("Rotation")]
    [SerializeField]
    private Vector3 rotationSpeed = new Vector3(35f, 70f, 25f);

    private Vector3 startPosition;
    private Vector3 burstDirection;
    private float elapsedTime;
    private bool isPlaying;

    public void Play(Vector3 origin, Vector3 direction)
    {
        startPosition = origin;
        burstDirection = direction.normalized;

        transform.position = origin;

        elapsedTime = 0f;
        isPlaying = true;

    }

    private void Update()
    {
        if (!isPlaying)
        {
            return;
        }

        elapsedTime += Time.deltaTime;

        transform.Rotate(
            rotationSpeed * Time.deltaTime,
            Space.Self
        );

        float t = Mathf.Clamp01(elapsedTime / burstDuration);

        // 最初は勢いよく終盤で柔らかく減速
        float easedT = 1f - Mathf.Pow(1f -t, 3f);

        transform.position =
            startPosition +
            burstDirection * burstDistance * easedT;
        
        if (t >= 1f)
        {
            isPlaying = false;
        }
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class CrystalFormationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private CrystalDustHeroMotion dustPrefab;

    [SerializeField]
    private Transform birthPoint;

    [SerializeField]
    private Camera targetCamera;

    public Transform BirthPoint => birthPoint;
    
    [Header("Spawn")]
    [Min(1)]
    [SerializeField]
    private int dustCount = 10;

    [Min(0f)]
    [SerializeField]
    private float spawnInterval = 0.055f;

    [Header("Rise")]
    [SerializeField]
    private float firstDistanceMultiplier = 0.55f;

    [SerializeField]
    private float lastDistanceMultiplier = 1.55f;

    public bool IsPlaying { get; private set; }

    private readonly List<CrystalDustHeroMotion> spawnedDust =
        new List<CrystalDustHeroMotion>();

    public void Play()
    {
        if (IsPlaying)
        {
            return;
        }
    
        if (birthPoint == null ||
            targetCamera == null ||
            dustPrefab == null)
        {
            Debug.LogWarning(
                $"{name}: Formationの参照が未設定です。");
            return;
        }

        IsPlaying = true;

        StartCoroutine(PlayFormation());
    }

    private IEnumerator PlayFormation()
    {
        spawnedDust.Clear();

        Vector3 riseDirection =
            targetCamera.transform.up.normalized;

        for (int i = 0; i < dustCount; i++)
        {
            float t =
                dustCount <= 1
                    ? 0f
                    : i / (float)(dustCount - 1);

            float distanceMultiplier =
                Mathf.Lerp(
                    firstDistanceMultiplier,
                    lastDistanceMultiplier,
                    t);

            CrystalDustHeroMotion dust =
                Instantiate(
                    dustPrefab,
                    birthPoint.position,
                    birthPoint.rotation,
                    transform);

            spawnedDust.Add(dust);

            dust.Play(
                birthPoint.position,
                riseDirection,
                distanceMultiplier);

            if (i < dustCount - 1)
            {
                yield return new WaitForSeconds(
                    spawnInterval);
            }
        }
        IsPlaying = false;
    }
}
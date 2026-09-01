using System;
using UnityEngine;

public sealed class CrystalEffectController : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField]
    private CrystalDustHeroMotion heroPrefab;

    [SerializeField]
    private Transform testSpawnPoint;

    [Header("Test")]
    [SerializeField]
    private Vector3 testBurstDirection = new Vector3(1f, 1f, 0f);

    [ContextMenu("Play Test Hero")]
    private void PlayTestHero()
    {
        if (heroPrefab == null || testSpawnPoint == null)
        {
            Debug.LogWarning("HeroPrefab または TestSpawnPoint が未設定");
            return;
        }

        CrystalDustHeroMotion hero =
            Instantiate(
                heroPrefab,
                testSpawnPoint.position,
                Quaternion.identity,
                transform
            );
        
        hero.Play(
            testSpawnPoint.position,
            testBurstDirection
        );
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            PlayTestHero();
        }
    }

}
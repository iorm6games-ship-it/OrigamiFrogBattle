using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class CrystalFormationController : MonoBehaviour
{
    private enum FormationPhase
    {
        Idle,
        Seed,
        Build,
        Peak,
        Condense,
        Complete
    }

    [Header("References")]
    [SerializeField]
    private CrystalDustHeroMotion dustPrefab;

    [SerializeField]
    private Transform birthPoint;

    public Transform BirthPoint => birthPoint;

    [Header("Dust")]
    [Min(1)]
    [SerializeField]
    private int dustCount = 10;

    [Header("Seed")]
    [Min(1)]
    [SerializeField]
    private int seedDustCount = 3;

    [Min(0f)]
    [SerializeField]
    private float seedSpawnInterval = 0.06f;

    [Header("Build")]
    [Min(0f)]
    [SerializeField]
    private float buildDuration = 0.55f;

    [Header("Peak")]
    [Min(0f)]
    [SerializeField]
    private float peakDuration = 0.18f;

    [Header("Condense")]
    [Min(0f)]
    [SerializeField]
    private float condenseDuration = 0.35f;

    public bool IsPlaying { get; private set; }

    private FormationPhase phase =
        FormationPhase.Idle;

    private readonly List<CrystalDustHeroMotion> spawnedDust =
        new List<CrystalDustHeroMotion>();

    public void Play()
    {
        if (IsPlaying)
        {
            return;
        }

        if (birthPoint == null ||
            dustPrefab == null)
        {
            Debug.LogWarning(
                $"{name}: Formationの参照が未設定です。");

            return;
        }

        StartCoroutine(
            PlayFormation());
    }

    private IEnumerator PlayFormation()
    {
        IsPlaying = true;

        spawnedDust.Clear();

        //
        // Seed
        //
        phase = FormationPhase.Seed;

        yield return PlaySeed();

        //
        // Build
        //
        phase = FormationPhase.Build;

        yield return PlayBuild();

        //
        // Peak
        //
        phase = FormationPhase.Peak;

        if (peakDuration > 0f)
        {
            yield return new WaitForSeconds(
                peakDuration);
        }

        //
        // Condense
        //
        phase = FormationPhase.Condense;

        // 今はまだ凝縮動作そのものは未実装。
        // 次の段階で spawnedDust 全体に
        // 凝縮開始を指示する。
        if (condenseDuration > 0f)
        {
            yield return new WaitForSeconds(
                condenseDuration);
        }

        //
        // Complete
        //
        phase = FormationPhase.Complete;

        IsPlaying = false;

        phase = FormationPhase.Idle;
    }

    private IEnumerator PlaySeed()
    {
        int actualSeedCount =
            Mathf.Clamp(
                seedDustCount,
                1,
                dustCount);

        for (int i = 0; i < actualSeedCount; i++)
        {
            SpawnDust(
                CrystalDustHeroMotion.VortexRole.Seed
            );

            if (i < actualSeedCount - 1 &&
                seedSpawnInterval > 0f)
            {
                yield return new WaitForSeconds(
                    seedSpawnInterval);
            }
        }
    }

    private IEnumerator PlayBuild()
    {
        int actualSeedCount =
            Mathf.Clamp(
                seedDustCount,
                1,
                dustCount);

        int buildDustCount =
            dustCount - actualSeedCount;

        if (buildDustCount <= 0)
        {
            yield break;
        }

        float interval =
            buildDustCount <= 1
                ? 0f
                : buildDuration /
                  (buildDustCount - 1);

        for (int i = 0; i < buildDustCount; i++)
        {
            SpawnDust(
                CrystalDustHeroMotion.VortexRole.Build
            );

            if (i < buildDustCount - 1 &&
                interval > 0f)
            {
                yield return new WaitForSeconds(
                    interval);
            }
        }
    }

    private void SpawnDust(
        CrystalDustHeroMotion.VortexRole role
    )
    {
        CrystalDustHeroMotion dust =
            Instantiate(
                dustPrefab,
                birthPoint.position,
                birthPoint.rotation,
                transform);

        spawnedDust.Add(dust);

        //
        // 現時点では既存Motionを動かすための仮方向。
        // ここは次に渦用の初期速度へ変更する。
        //
        
        dust.Play(
            birthPoint.position,
            birthPoint.up,
            role);
    }
}
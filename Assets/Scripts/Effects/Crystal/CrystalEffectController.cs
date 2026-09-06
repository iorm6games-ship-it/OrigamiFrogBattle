using System.Collections;
using UnityEngine;

public sealed class CrystalEffectController : MonoBehaviour
{
    [Header("Formation")]
    [SerializeField]
    private CrystalFormationController centerFormation;

    [SerializeField]
    private CrystalFormationController[] cornerFormations;

    [Header("Timing")]
    [Min(0f)]
    [SerializeField]
    private float cornerFormationDelay = 0.14f;

    private Coroutine sequenceCoroutine;
    public Transform CenterBirthPoint =>
        centerFormation != null
            ? centerFormation.BirthPoint
            : null;
            
    private void Update()
    {
        // 手動確認用
        if (Input.GetKeyDown(KeyCode.Space) &&
            sequenceCoroutine == null)
        {
            sequenceCoroutine =
                StartCoroutine(
                    PlayCrystalFormationSequence());
        }
    }

    public IEnumerator PlaySequence()
    {
        if (sequenceCoroutine == null)
        {
            sequenceCoroutine =
                StartCoroutine(
                    PlayCrystalFormationSequence());
        }

        yield return sequenceCoroutine;
    }

    private IEnumerator PlayCrystalFormationSequence()
    {
        // まず中央
        centerFormation?.Play();

        // わずかに時間差
        yield return new WaitForSeconds(
            cornerFormationDelay);

        // その後、四隅
        if (cornerFormations != null)
        {
            foreach (
                CrystalFormationController formation
                in cornerFormations)
            {
                formation?.Play();
            }
        }

        yield return new WaitUntil(
            AreAllFormationComplete);

        sequenceCoroutine = null;
    }

    private bool AreAllFormationComplete()
    {
        if (centerFormation != null &&
            centerFormation.IsPlaying)
        {
            return false;
        }
        if (cornerFormations != null)
        {
            foreach (
                CrystalFormationController formation
                in cornerFormations)
            {
                if (formation != null &&
                    formation.IsPlaying)
                {
                    return false;
                }
            }
        }
        return true;
    }
}
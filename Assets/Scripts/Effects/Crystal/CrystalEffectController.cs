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

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            PlayTestSequence();
        }
    }

    private void PlayTestSequence()
    {
        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
        }

        sequenceCoroutine =
            StartCoroutine(
                PlayCrystalFormationSequence());
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

        sequenceCoroutine = null;
    }
}
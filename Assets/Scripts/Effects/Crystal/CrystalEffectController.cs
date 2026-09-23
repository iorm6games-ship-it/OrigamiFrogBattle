using System.Collections;
using UnityEngine;

public sealed class CrystalEffectController : MonoBehaviour
{
    [Header("Formation")]
    [SerializeField]
    private CrystalFormationController centerFormation;

    [SerializeField]
    private CrystalFormationController[] cornerFormations;

    [Header("Formation Timing")]
    [Min(0f)]
    [SerializeField]
    private float cornerFormationDelay = 0.14f;

    [Header("Completed Lift Targets")]
    [Tooltip("CrystalLiftTargets/Center")]
    [SerializeField]
    private Transform centerLiftTarget;

    [Tooltip("Element 0～3 を Corner_01～Corner_04 に対応させる。")]
    [SerializeField]
    private Transform[] cornerLiftTargets;

    [Header("Completed Lift Sequence")]
    [Tooltip("中央Crystalが上昇を開始してから、四隅が追従し始めるまでの時間。")]
    [Min(0f)]
    [SerializeField]
    private float cornerLiftFollowDelay = 0.10f;

    [Tooltip("四隅4個を完全同時にせず、ごく小さく反応差を付ける。0でも可。")]
    [Min(0f)]
    [SerializeField]
    private float cornerLiftStagger = 0.015f;
    [Header("Energy Transfer")]
    [SerializeField]
    private CrystalEnergyTransferController energyTransferController;

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
        SetExternalLiftControl(
            true);

        //
        // 1. Formation自体は従来どおり
        //    中央 → 少し遅れて四隅。
        //
        centerFormation?.Play();

        if (cornerFormationDelay > 0f)
        {
            yield return new WaitForSeconds(
                cornerFormationDelay);
        }

        if (cornerFormations != null)
        {
            foreach (
                CrystalFormationController formation
                in cornerFormations)
            {
                formation?.Play();
            }
        }

        //
        // 2. 中央Crystalが「組み上がった」瞬間に、
        //    完成後Liftを先に開始する。
        //
        if (centerFormation != null)
        {
            yield return new WaitUntil(
                () =>
                    IsReadyForLift(
                        centerFormation));

            if (centerFormation.IsCrystalBuilt)
            {
                Debug.Log(
                    $"[CrystalLift] Center start -> " +
                    $"{(centerLiftTarget != null ? centerLiftTarget.name : "null")} " +
                    $"{(centerLiftTarget != null ? centerLiftTarget.position.ToString("F3") : "-")}",
                    this);

                StartCoroutine(
                    centerFormation.PlayCompletedLiftTo(
                        centerLiftTarget));
            }
        }

        //
        // 3. 中央が先に動いたことを読ませる短い間。
        //
        if (cornerLiftFollowDelay > 0f)
        {
            yield return new WaitForSeconds(
                cornerLiftFollowDelay);
        }

        //
        // 4. 四隅のCrystalがまだ形成途中なら、
        //    形成完了まで待ってから追従させる。
        //
        yield return new WaitUntil(
            AreAllCornerCrystalsReadyForLift);

        //
        // 5. 四隅は「順番に上がる演出」にはしない。
        //    ほぼ同時。ただし数フレームだけ反応差を付ける。
        //
        if (cornerFormations != null)
        {
            for (int i = 0;
                 i < cornerFormations.Length;
                 i++)
            {
                CrystalFormationController formation =
                    cornerFormations[i];

                if (formation != null &&
                    formation.IsCrystalBuilt)
                {
                    Transform liftTarget =
                        GetCornerLiftTarget(i);

                    Debug.Log(
                        $"[CrystalLift] Corner {i} start -> " +
                        $"{(liftTarget != null ? liftTarget.name : "null")} " +
                        $"{(liftTarget != null ? liftTarget.position.ToString("F3") : "-")}",
                        this);

                    StartCoroutine(
                        formation.PlayCompletedLiftTo(
                            liftTarget));
                }

                if (cornerLiftStagger > 0f &&
                    i < cornerFormations.Length - 1)
                {
                    yield return new WaitForSeconds(
                        cornerLiftStagger);
                }
            }
        }

        //
        // 6. 5個のCrystalの先行Lift完了を待つ。
        //    この間、Paper側の後続シーケンスはまだ始まらない。
        //
        yield return new WaitUntil(
            AreAllCompletedLiftsComplete);

        //
        // 7. Formation側のCondenseGlow Fade等の
        //    後処理完了を待つ。
        //
        yield return new WaitUntil(
            AreAllFormationComplete);

        SetExternalLiftControl(
            false);

        //
        // 8. 5本のCrystalが完成・浮遊状態に入り、
        //    Formation VFXも消えたところで
        //    Corner → CenterのEnergy Transferへ移行。
        //
        if (TryBindEnergyTransfer())
        {
            yield return
                energyTransferController.Play();
        }

        sequenceCoroutine = null;
    }
    private void SetExternalLiftControl(
        bool enabled)
    {
        centerFormation?.SetExternalLiftControl(
            enabled);

        if (cornerFormations == null)
        {
            return;
        }

        foreach (
            CrystalFormationController formation
            in cornerFormations)
        {
            formation?.SetExternalLiftControl(
                enabled);
        }
    }
    private Transform GetCornerLiftTarget(
        int index)
    {
        if (cornerLiftTargets == null ||
            index < 0 ||
            index >= cornerLiftTargets.Length)
        {
            return null;
        }

        return cornerLiftTargets[index];
    }

    private bool IsReadyForLift(
        CrystalFormationController formation)
    {
        if (formation == null)
        {
            return true;
        }

        //
        // IsPlaying=falseのままなら参照不足等でPlayできなかった可能性がある。
        // その場合に無限待ちしない。
        //
        return
            formation.IsCrystalBuilt ||
            !formation.IsPlaying;
    }

    private bool AreAllCornerCrystalsReadyForLift()
    {
        if (cornerFormations == null)
        {
            return true;
        }

        foreach (
            CrystalFormationController formation
            in cornerFormations)
        {
            if (formation == null)
            {
                continue;
            }

            if (formation.IsPlaying &&
                !formation.IsCrystalBuilt)
            {
                return false;
            }
        }

        return true;
    }

    private bool AreAllCompletedLiftsComplete()
    {
        if (centerFormation != null &&
            centerFormation.IsCrystalBuilt &&
            !centerFormation.IsCompletedLiftComplete)
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
                    formation.IsCrystalBuilt &&
                    !formation.IsCompletedLiftComplete)
                {
                    return false;
                }
            }
        }

        return true;
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

    private bool TryBindEnergyTransfer()
    {
        if (energyTransferController == null)
        {
            Debug.LogWarning(
                $"{name}: Energy Transfer Controllerが未設定です。",
                this);

            return false;
        }

        if (centerFormation == null ||
            centerFormation.AssemblyInstance == null)
        {
            Debug.LogWarning(
                $"{name}: Center Crystal Assemblyがありません。",
                this);

            return false;
        }

        if (cornerFormations == null ||
            cornerFormations.Length != 4)
        {
            Debug.LogWarning(
                $"{name}: Corner Formationは4本必要です。",
                this);

            return false;
        }

        CrystalAssemblyController[] corners =
            new CrystalAssemblyController[
                cornerFormations.Length];

        for (int i = 0;
            i < cornerFormations.Length;
            i++)
        {
            CrystalFormationController formation =
                cornerFormations[i];

            if (formation == null ||
                formation.AssemblyInstance == null)
            {
                Debug.LogWarning(
                    $"{name}: Corner[{i}]のAssemblyがありません。",
                    this);

                return false;
            }

            corners[i] =
                formation.AssemblyInstance;
        }

        return
            energyTransferController.BindCrystals(
                centerFormation.AssemblyInstance,
                corners);
    }
}

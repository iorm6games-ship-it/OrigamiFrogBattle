using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CrystalEnergyTransferController : MonoBehaviour
{
    public enum TransferPhase
    {
        Idle,
        Charging,
        Converging,
        CenterCharged,
        PaperTransfer,
        Complete
    }

    [Header("Crystal References")]

    [SerializeField]
    private CrystalAssemblyController centerCrystal;

    [SerializeField]
    private CrystalAssemblyController[] cornerCrystals =
        new CrystalAssemblyController[4];


    [Header("Paper")]

    [SerializeField]
    private Transform paperReceivePoint;


    [Header("Timing")]

    [Min(0f)]
    [SerializeField]
    private float chargeDuration = 0.20f;

    [Min(0f)]
    [SerializeField]
    private float cornerBeamStagger = 0.035f;

    [Min(0.01f)]
    [SerializeField]
    private float cornerBeamTravelDuration = 0.20f;

    [Min(0f)]
    [SerializeField]
    private float centerChargeHold = 0.10f;

    [Min(0.01f)]
    [SerializeField]
    private float paperBeamTravelDuration = 0.12f;

    [Min(0f)]
    [SerializeField]
    private float paperReactionDelay = 0.08f;


    [Header("Beam")]

    [SerializeField]
    private CrystalEnergyBeam[] cornerBeams =
        new CrystalEnergyBeam[4];


    [Header("Beam Test")]

    [SerializeField]
    private CrystalEnergyBeam testBeam;


    public TransferPhase Phase { get; private set; } =
        TransferPhase.Idle;

    public bool IsComplete =>
        Phase == TransferPhase.Complete;


    public IEnumerator Play()
    {
        if (!ValidateReferences())
        {
            yield break;
        }

        Phase =
            TransferPhase.Charging;

        yield return
            PlayCornerCharge();


        Phase =
            TransferPhase.Converging;

        yield return
            PlayCornerConvergence();


        Phase =
            TransferPhase.CenterCharged;

        if (centerChargeHold > 0f)
        {
            yield return
                new WaitForSeconds(
                    centerChargeHold);
        }


        Phase =
            TransferPhase.PaperTransfer;

        yield return
            PlayPaperTransfer();


        if (paperReactionDelay > 0f)
        {
            yield return
                new WaitForSeconds(
                    paperReactionDelay);
        }


        Phase =
            TransferPhase.Complete;
    }


    private IEnumerator PlayCornerCharge()
    {
        //
        // 次段階:
        // Corner先端のEmission / Sparkle集束
        //
        if (chargeDuration > 0f)
        {
            yield return
                new WaitForSeconds(
                    chargeDuration);
        }
    }


    private IEnumerator PlayCornerConvergence()
    {
        //
        // 現在はCorner_01 → Centerの
        // 一本だけをテストする。
        //
        if (testBeam == null)
        {
            Debug.LogWarning(
                $"{name}: Test Beamが未設定です。",
                this);

            yield break;
        }

        testBeam.PlayReveal();

        yield return
            new WaitForSeconds(
                cornerBeamTravelDuration);
    }


    private IEnumerator PlayPaperTransfer()
    {
        //
        // まだ未実装。
        // Center → PaperのBeamを
        // 次段階で追加する。
        //

        yield return
            new WaitForSeconds(
                paperBeamTravelDuration);
    }


    private bool ValidateReferences()
    {
        if (centerCrystal == null)
        {
            Debug.LogError(
                $"{name}: Center Crystalが未設定です。",
                this);

            return false;
        }

        if (centerCrystal.EnergyTip == null)
        {
            Debug.LogError(
                $"{name}: Center CrystalのEnergyTipが未設定です。",
                this);

            return false;
        }

        if (cornerCrystals == null ||
            cornerCrystals.Length != 4)
        {
            Debug.LogError(
                $"{name}: Corner Crystalは4本必要です。",
                this);

            return false;
        }

        for (int i = 0;
             i < cornerCrystals.Length;
             i++)
        {
            if (cornerCrystals[i] == null ||
                cornerCrystals[i].EnergyTip == null)
            {
                Debug.LogError(
                    $"{name}: Corner Crystal[{i}] またはEnergyTipが未設定です。",
                    this);

                return false;
            }
        }

        //
        // Paper側はまだ未実装なので
        // 現段階では必須チェックしない。
        //
        return true;
    }


    public bool BindCrystals(
        CrystalAssemblyController center,
        CrystalAssemblyController[] corners)
    {
        if (center == null ||
            center.EnergyTip == null)
        {
            Debug.LogError(
                $"{name}: Center CrystalまたはEnergyTipがありません。",
                this);

            return false;
        }

        if (corners == null ||
            corners.Length != 4)
        {
            Debug.LogError(
                $"{name}: Corner Crystalは4本必要です。",
                this);

            return false;
        }

        for (int i = 0;
             i < corners.Length;
             i++)
        {
            if (corners[i] == null ||
                corners[i].EnergyTip == null)
            {
                Debug.LogError(
                    $"{name}: Corner[{i}]またはEnergyTipがありません。",
                    this);

                return false;
            }
        }

        centerCrystal =
            center;

        cornerCrystals =
            corners;


        //
        // 今はCorner_01だけをテスト。
        //
        if (testBeam != null)
        {
            testBeam.SetEndpoints(
                cornerCrystals[0].EnergyTip,
                centerCrystal.EnergyTip);

            testBeam.SetVisibleImmediate(
                false);
        }

        return true;
    }
}
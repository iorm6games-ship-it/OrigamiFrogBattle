using UnityEngine;

/// <summary>
/// CrystalDustHeroMotionの通常Vortexを、Anchor形成時だけ引き継ぐ。
/// LateUpdateで位置を上書きするのではなく、BeginGather時に元MotionをDisableして
/// このComponentが最後の吸い込みを担当する。
/// </summary>
[DisallowMultipleComponent]
public sealed class CrystalDustAnchorGatherController : MonoBehaviour
{
    private CrystalDustHeroMotion sourceMotion;

    private Vector3 startPosition;
    private Vector3 startScale;
    private Quaternion startRotation;

    private Vector3 targetPosition;
    private Vector3 axis;
    private Vector3 curveDirection;

    private float duration;
    private float delay;
    private float curveAmount;
    private float elapsed;

    private bool playing;

    public void BeginGather(
        CrystalDustHeroMotion motion,
        Vector3 target,
        Vector3 gatherAxis,
        float gatherDuration,
        float startDelay,
        float curveStrength)
    {
        sourceMotion = motion;

        startPosition =
            transform.position;

        startScale =
            transform.localScale;

        startRotation =
            transform.rotation;

        targetPosition =
            target;

        axis =
            gatherAxis.sqrMagnitude > 0.000001f
                ? gatherAxis.normalized
                : Vector3.up;

        duration =
            Mathf.Max(
                0.05f,
                gatherDuration);

        delay =
            Mathf.Clamp(
                startDelay,
                0f,
                duration * 0.8f);

        Vector3 toTarget =
            targetPosition -
            startPosition;

        Vector3 planar =
            Vector3.ProjectOnPlane(
                toTarget,
                axis);

        curveDirection =
            Vector3.Cross(
                axis,
                planar);

        if (curveDirection.sqrMagnitude <
            0.000001f)
        {
            curveDirection =
                Vector3.Cross(
                    axis,
                    Vector3.right);

            if (curveDirection.sqrMagnitude <
                0.000001f)
            {
                curveDirection =
                    Vector3.forward;
            }
        }

        curveDirection.Normalize();

        curveAmount =
            Vector3.Distance(
                startPosition,
                targetPosition) *
            Mathf.Clamp01(
                curveStrength);

        elapsed = 0f;
        playing = true;

        if (sourceMotion != null)
        {
            sourceMotion.enabled = false;
        }
    }

    private void Update()
    {
        if (!playing)
        {
            return;
        }

        elapsed +=
            Time.deltaTime;

        if (elapsed < delay)
        {
            return;
        }

        float activeDuration =
            Mathf.Max(
                0.01f,
                duration -
                delay);

        float t =
            Mathf.Clamp01(
                (elapsed - delay) /
                activeDuration);

        //
        // 最初は渦の勢いを少し残し、
        // 最後にCrystal_Lowerへ吸い込まれる。
        //
        float moveT =
            1f -
            Mathf.Pow(
                1f - t,
                3f);

        float arc =
            Mathf.Sin(
                moveT *
                Mathf.PI) *
            curveAmount *
            (1f - 0.35f * moveT);

        transform.position =
            Vector3.LerpUnclamped(
                startPosition,
                targetPosition,
                moveT) +
            curveDirection *
            arc;

        //
        // ターゲットへ近づくほど回転も静かにする。
        //
        Quaternion settleRotation =
            Quaternion.LookRotation(
                Vector3.ProjectOnPlane(
                    targetPosition -
                    startPosition,
                    axis).sqrMagnitude >
                0.000001f
                    ? Vector3.ProjectOnPlane(
                        targetPosition -
                        startPosition,
                        axis).normalized
                    : Vector3.forward,
                axis);

        transform.rotation =
            Quaternion.Slerp(
                startRotation,
                settleRotation,
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t));

        //
        // Dustが消える代わりにLowerが実体化する。
        // 後半だけ急速に小さくして「吸収」感を出す。
        //
        float shrinkT =
            Mathf.InverseLerp(
                0.45f,
                1f,
                t);

        shrinkT =
            Mathf.SmoothStep(
                0f,
                1f,
                shrinkT);

        transform.localScale =
            Vector3.Lerp(
                startScale,
                startScale * 0.035f,
                shrinkT);

        if (t >= 1f)
        {
            playing = false;
            Destroy(gameObject);
        }
    }
}

using System.Collections;
using UnityEngine;

public sealed class SummonCameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Camera targetCamera;

    [Header("Zoom")]
    [SerializeField]
    private float zoomDuration = 0.35f;

    [SerializeField]
    private float zoomFov = 42f;

    private float defaultFov;
    private Coroutine zoomCoroutine;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        if (targetCamera != null)
        {
            defaultFov = targetCamera.fieldOfView;
        }
    }

    public IEnumerator ZoomIn()
    {
        if (targetCamera == null)
        {
            yield break;
        }

        float startFov =
            targetCamera.fieldOfView;

        float elapsed = 0f;

        while (elapsed < zoomDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / zoomDuration
                );

            float eased =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

            targetCamera.fieldOfView =
                Mathf.Lerp(
                    startFov,
                    zoomFov,
                    eased
                );

            yield return null;
        }

        targetCamera.fieldOfView =
            zoomFov;
    }

    public IEnumerator ZoomOut()
    {
        if (targetCamera == null)
        {
            yield break;
        }

        float startFov =
            targetCamera.fieldOfView;

        float elapsed = 0f;

        while (elapsed < zoomDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / zoomDuration
                );

            float eased =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

            targetCamera.fieldOfView =
                Mathf.Lerp(
                    startFov,
                    defaultFov,
                    eased
                );

            yield return null;
        }

        targetCamera.fieldOfView =
            defaultFov;
    }
}
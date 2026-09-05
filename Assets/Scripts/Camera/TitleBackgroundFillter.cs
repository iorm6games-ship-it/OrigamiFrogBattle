using UnityEngine;

[ExecuteAlways]
public sealed class TitleBackgroundFillter : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Camera targetCamera;

    [SerializeField]
    private Transform backgroundPlane;

    [Header("Layout")]
    [Min(0.01f)]
    [SerializeField]
    private float distance = 100f;

    [Range(1f, 1.2f)]
    [SerializeField]
    private float overscan = 1.05f;

    private void LateUpdate()
    {
        Fit();
    }

    private void Fit()
    {
        if (targetCamera == null || backgroundPlane == null)
        {
            return;
        }
        float safeDistance = Mathf.Clamp(
            distance,
            targetCamera.nearClipPlane + 0.01f,
            targetCamera.farClipPlane - 0.01f);
        
        float width;
        float height;

        if (targetCamera.orthographic)
        {
            height = targetCamera.orthographicSize * 2f;
            width = height * targetCamera.aspect;
        }
        else
        {
            float halfFovRadians =
                targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            height =
                2f * safeDistance * Mathf.Tan(halfFovRadians);
            width = height * targetCamera.aspect;
        }

        backgroundPlane.localPosition =
            new Vector3(0f, 0f, safeDistance);
        
        backgroundPlane.localRotation =
            Quaternion.identity;
        
        backgroundPlane.localScale =
            new Vector3(
                width * overscan,
                height * overscan,
                1f);
    }
}
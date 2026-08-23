using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public sealed class SummonCameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Camera targetCamera;

    [Header("Zoom")]
    [SerializeField]
    private float zoomDuration = 0.6f;

    [SerializeField]
    private float zoomFov = 42f;

    [Header("Fold Zoom")]
    [SerializeField]
    private float foldZoomFov = 35f;

    [SerializeField]
    private float foldDuration = 0.5f;

    [SerializeField]
    private Transform foldCameraPoint;
    [SerializeField]
    private float foldZoomDuration = 0.5f;

    [Header("Late Fold Zoom")]
    [SerializeField]
    private float lateFoldZoomDelay = 2.0f;

    [SerializeField]
    private float lateFoldZoomDuration = 1.2f;

    [SerializeField]
    private float lateFoldZoomFov = 21f;

    [Header("Fold Center Tracking")]
    [SerializeField]
    private float foldCenterSmoothTime = 0.35f;

    private SkinnedMeshRenderer foldFocusSkinnedRenderer;
    private Mesh bakedFoldMesh;

    private Renderer foldFocusRenderer;
    private bool trackFoldCenter;
    private Vector3 foldCenterVelocity;
    private float defaultFov;
    private Vector3 foldTrackingStartRendererPosition;

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
    private IEnumerator ZoomTo(
        float targetFov,
        float duration
    )
    {
        if (targetCamera == null)
        {
            yield break;
        }

        float startFov =
            targetCamera.fieldOfView;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / duration
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
                    targetFov,
                    eased
                );

            yield return null;
        }

        targetCamera.fieldOfView =
            targetFov;
    }    
    public IEnumerator ZoomIn()
    {
        yield return ZoomTo(
            zoomFov,
            zoomDuration
        );
    }

    public IEnumerator ZoomForFold()
    {
        Debug.Log(
            $"ZoomForFold start: {targetCamera.fieldOfView} -> {foldZoomFov}"
        );
        yield return ZoomTo(
            foldZoomFov,
            foldDuration
        );
    }
    
    public IEnumerator ZoomOut()
    {
       yield return ZoomTo(
            defaultFov,
            zoomDuration
       );
    }

    public IEnumerator MoveForFold()
    {
        if (targetCamera == null ||
            foldCameraPoint == null)
        {
            yield break;
        }

        Transform cameraTransform =
            targetCamera.transform;

        Vector3 startPosition =
            cameraTransform.position;

        Quaternion startRotation =
            cameraTransform.rotation;

        float startFov =
            targetCamera.fieldOfView;

        float elapsed = 0f;

        while (elapsed < foldZoomDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / foldZoomDuration
                );

            float eased =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

            cameraTransform.position =
                Vector3.Lerp(
                    startPosition,
                    foldCameraPoint.position,
                    eased
                );

            cameraTransform.rotation =
                Quaternion.Slerp(
                    startRotation,
                    foldCameraPoint.rotation,
                    eased
                );

            targetCamera.fieldOfView =
                Mathf.Lerp(
                    startFov,
                    foldZoomFov,
                    eased
                );

            yield return null;
        }

        cameraTransform.position =
            foldCameraPoint.position;

        cameraTransform.rotation =
            foldCameraPoint.rotation;

        targetCamera.fieldOfView =
            foldZoomFov;
    }
    public IEnumerator ZoomLateFold()
    {
        if (targetCamera == null)
        {
            yield break;
        }

        float elapsed = 0f;

        // 折り畳み前半は今の画角を維持
        while (elapsed < lateFoldZoomDelay)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 後半だけ、完成するカエルへ寄る
        yield return ZoomTo(
            lateFoldZoomFov,
            lateFoldZoomDuration
        );
    }
    public void StartFoldCenterTracking(
        Renderer targetRenderer
    )
    {
        foldFocusRenderer =
            targetRenderer;

        foldFocusSkinnedRenderer =
            targetRenderer as SkinnedMeshRenderer;

        trackFoldCenter =
            foldFocusSkinnedRenderer != null;

        foldCenterVelocity =
            Vector3.zero;

        if (foldFocusSkinnedRenderer != null)
        {
            foldTrackingStartRendererPosition =
                foldFocusSkinnedRenderer.transform.position;
        }

        if (bakedFoldMesh == null)
        {
            bakedFoldMesh =
                new Mesh();
        }
    }

    public void StopFoldCenterTracking()
    {
        trackFoldCenter = false;
        foldFocusRenderer = null;
        foldCenterVelocity = Vector3.zero;
    }
    private void LateUpdate()
    {
        if (!trackFoldCenter ||
            foldFocusSkinnedRenderer == null ||
            targetCamera == null)
        {
            return;
        }

        // 現在のBlendShape変形結果を取得
        foldFocusSkinnedRenderer.BakeMesh(
            bakedFoldMesh
        );

        Vector3[] vertices =
            bakedFoldMesh.vertices;

        if (vertices == null ||
            vertices.Length == 0)
        {
            return;
        }

        float minX =
            float.PositiveInfinity;

        float maxX =
            float.NegativeInfinity;

        float minY =
            float.PositiveInfinity;

        float maxY =
            float.NegativeInfinity;

        Transform meshTransform =
            foldFocusSkinnedRenderer.transform;

        // 折り開始後のRenderer全体の移動量。
        // これを頂点から差し引くことで、
        // 浮遊には追従せず、折りによる形状変化だけを追う。
        Vector3 floatingOffset =
            meshTransform.position -
            foldTrackingStartRendererPosition;

        for (int i = 0;
            i < vertices.Length;
            i++)
        {
            Vector3 worldPosition =
                meshTransform.TransformPoint(
                    vertices[i]
                );

            // Renderer全体の浮遊移動は追わない
            worldPosition -= floatingOffset;

            Vector3 viewport =
                targetCamera.WorldToViewportPoint(
                    worldPosition
                );

            if (viewport.z <= 0f)
            {
                continue;
            }

            minX =
                Mathf.Min(
                    minX,
                    viewport.x
                );

            maxX =
                Mathf.Max(
                    maxX,
                    viewport.x
                );

            minY =
                Mathf.Min(
                    minY,
                    viewport.y
                );

            maxY =
                Mathf.Max(
                    maxY,
                    viewport.y
                );
        }

        if (float.IsInfinity(minX) ||
            float.IsInfinity(maxX) ||
            float.IsInfinity(minY) ||
            float.IsInfinity(maxY))
        {
            return;
        }

        // 変形後メッシュの、画面上の見た目の中心
        float visualCenterX =
            (minX + maxX) * 0.5f;

        float visualCenterY =
            (minY + maxY) * 0.5f;

        float viewportErrorX =
            visualCenterX - 0.5f;

        float viewportErrorY =
            visualCenterY - 0.5f;

        // 対象までの奥行き
        Vector3 trackingBoundsCenter =
            foldFocusSkinnedRenderer.bounds.center -
            floatingOffset;

        float depth =
            Vector3.Dot(
                trackingBoundsCenter -
                targetCamera.transform.position,
                targetCamera.transform.forward
            );

        if (depth <= 0f)
        {
            return;
        }

        // Viewport上のズレを
        // World座標の距離へ変換
        float halfHeight =
            Mathf.Tan(
                targetCamera.fieldOfView *
                0.5f *
                Mathf.Deg2Rad
            ) *
            depth;

        float halfWidth =
            halfHeight *
            targetCamera.aspect;

        float worldOffsetX =
            viewportErrorX *
            2f *
            halfWidth;

        float worldOffsetY =
            viewportErrorY *
            2f *
            halfHeight;

        Transform cameraTransform =
            targetCamera.transform;

        Vector3 desiredPosition =
            cameraTransform.position +
            cameraTransform.right *
            worldOffsetX +
            cameraTransform.up *
            worldOffsetY;

        cameraTransform.position =
            Vector3.SmoothDamp(
                cameraTransform.position,
                desiredPosition,
                ref foldCenterVelocity,
                foldCenterSmoothTime
            );
    }
}
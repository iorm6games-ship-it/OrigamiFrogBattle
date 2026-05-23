using UnityEngine;
using System.Collections;


public class CameraController: MonoBehaviour
{
	public enum CameraState
	{
        Follow,
        ResultLosser,
        ResultWinner
    }
	[Header("Target")]
	[SerializeField] private Transform player1;
    [SerializeField] private Transform player2;

    [Header("Camera")]
    [SerializeField] private Camera cam;
    [SerializeField] private float minDistance = 7f;
    [SerializeField] private float maxDistance = 30f;
    [SerializeField] private float padding = 4f;

	[Header("Follow")]
	[SerializeField] private float followSmooth = 5f;
	[SerializeField] private Vector3 centerOffset = new Vector3(0f, 1.2f, 0f);

	[Header("Result")]
	[SerializeField] private float resultDistance = 5f;
	[SerializeField] private float resultMoveDuration = 0.8f;
	[SerializeField] private float resultHoldTime = 1.2f;

	[Header("Viewport Safe Area")]
	[SerializeField] private float safeLeft = 0.05f;
	[SerializeField] private float safeRight = 0.95f;
	[SerializeField] private float safeTop = 0.92f;
	[SerializeField] private float safeBottom = 0.08f;

	[SerializeField] private float zoomOutBoost = 5f;
	[SerializeField] private float distanceDamping = 0.15f;
	[SerializeField] private float distanceChangeThreshold = 0.5f;
	[SerializeField] private float minDistanceChangeForUpdate = 0.1f;

	private Vector3 cameraVelocity = Vector3.zero;
	private CameraState state = CameraState.Follow;
	private Coroutine resultCoroutine;

	private float currentDistance;
	private float distanceVelocity = 0f;
	private float targetDistanceSmoothBuffer = 0f;

	// Callback for when Winner is focused
	private System.Action onWinnerFocused;
	public void SetOnWinnerFocusedCallback(System.Action callback)
	{
		onWinnerFocused = callback;
	}

    private void Start()
    {
        if (cam == null)
        {
            cam = Camera.main;
        }

        Vector3 center = (player1.position + player2.position) * 0.5f + centerOffset;
        currentDistance = Vector3.Distance(transform.position, center);
    }
    private void Reset()
	{
		cam = Camera.main;
	}

	private void LateUpdate()
	{
		if (state != CameraState.Follow) return;
		if (player1 == null || player2 == null || cam == null) return;

		FollowPlayers();
    }

    private void FollowPlayers()
    {

        Bounds bounds1 = GetTargetBounds(player1);
        Bounds bounds2 = GetTargetBounds(player2);

        Bounds combinedBounds = bounds1;
        combinedBounds.Encapsulate(bounds2);

        Vector3 targetCenter = combinedBounds.center + centerOffset;

        float verticalFovRad = cam.fieldOfView * Mathf.Deg2Rad;

        Vector3 extents = combinedBounds.extents;

        float requiredHeight = extents.y + padding;
        float requiredWidth = Mathf.Max(extents.x, extents.z) + padding;

        float distanceByHeight =
            requiredHeight / Mathf.Tan(verticalFovRad * 0.5f);

        float distanceByWidth =
            requiredWidth / (Mathf.Tan(verticalFovRad * 0.5f) * cam.aspect);

        float targetDistance = Mathf.Max(distanceByHeight, distanceByWidth);

        bool outOfSafeArea =
            IsBoundsOutsideSafeArea(bounds1) ||
            IsBoundsOutsideSafeArea(bounds2);
        bool canAutoZoom =
            GameManager.currentState == GameManager.GameState.Playing;
        if (canAutoZoom && outOfSafeArea)
        {
            targetDistance += zoomOutBoost;
        }

        targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);

        // Improved hysteresis filter to prevent oscillation
        float distanceDelta = Mathf.Abs(targetDistance - targetDistanceSmoothBuffer);

        // If change is very small, don't update
        if (distanceDelta < minDistanceChangeForUpdate)
        {
            // Keep current buffer, no update
        }
        // If change is significant, snap to new target
        else if (distanceDelta > distanceChangeThreshold)
        {
            targetDistanceSmoothBuffer = targetDistance;
        }
        // If change is medium, smooth transition
        else
        {
            targetDistanceSmoothBuffer = Mathf.Lerp(targetDistanceSmoothBuffer, targetDistance, distanceDamping);
        }

        // use followSmooth to control responsiveness (higher -> snappier)
        float smoothTime = Mathf.Max(0.01f, 1f / Mathf.Max(0.0001f, followSmooth));

        currentDistance = Mathf.SmoothDamp(
            currentDistance,
            targetDistanceSmoothBuffer,
            ref distanceVelocity,
            smoothTime,
            maxDistance - minDistance  // Max speed to prevent jerky motion
        );

        Vector3 targetPosition = targetCenter - transform.forward * currentDistance;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref cameraVelocity,
            smoothTime
        );
    }

    private bool IsBoundsWellInsideSafeArea(Bounds bounds)
    {
        Vector3[] corners = GetBoundsCorners(bounds);
		float innerLeft = 0.12f;
        float innerRight = 0.88f;
		float innerBottom = 0.15f;
        float innerTop = 0.85f;

        foreach (Vector3 corner in corners)
        {
            Vector3 vp = cam.WorldToViewportPoint(corner);
            if (vp.z < 0f) return false;
            if (vp.x < innerLeft || vp.x > innerRight ||
                vp.y < innerBottom || vp.y > innerTop)
            {
                return false;
            }
        }
        return true;
    }

    private float GetCurrentDistanceFromCenter()
	{
		Vector3 center = (player1.position + player2.position) * 0.5f + centerOffset;
		return Vector3.Distance(transform.position, center);

    }
    public void PlayResultSequence(Transform loser, Transform winner)
	{
		if (resultCoroutine != null)
        {
            StopCoroutine(resultCoroutine);
        }

		resultCoroutine = StartCoroutine(
            ResultSequence(
                loser,
                winner
             )
        );

    }

	private IEnumerator ResultSequence(
		Transform loser,
		Transform winner
	)
	{
		state = CameraState.ResultLosser;
		yield return FocusTarget(loser);
		yield return new WaitForSeconds(resultHoldTime);

		state = CameraState.ResultWinner;
		yield return FocusTarget(winner);

		// Call Winner focused callback
		onWinnerFocused?.Invoke();

		yield return new WaitForSeconds(resultHoldTime);

		state = CameraState.Follow;
		resultCoroutine = null;
	}

	private IEnumerator FocusTarget(Transform target)
	{
		if (target == null) yield break;

		Vector3 startPos = transform.position;
		Vector3 targetCenter = target.position + centerOffset;
		Vector3 endPos = targetCenter - transform.forward * resultDistance;

		float elapsed = 0f;

		while (elapsed < resultMoveDuration)
		{
			elapsed += Time.deltaTime;
			float t = Mathf.Clamp01(elapsed / resultMoveDuration);

            // ease in/out
            t = t * t * (3f - 2f * t);

			transform.position = Vector3.Lerp(startPos, endPos, t);

            yield return null;
        }
		transform.position = endPos;
    }

	private Bounds GetTargetBounds(Transform target)
    {
		Renderer[] renderers = target.GetComponentsInChildren<Renderer>();

		if (renderers == null || renderers.Length == 0)
		{
			return new Bounds(target.position, Vector3.one);
		}

		Bounds bounds = renderers[0].bounds;

		for (int i = 1; i < renderers.Length; i++)
		{
			bounds.Encapsulate(renderers[i].bounds);
		}

		return bounds;
    }

	private Vector3[] GetBoundsCorners(Bounds bounds)
    {
     
        Vector3 extents = bounds.extents;
        Vector3 center = bounds.center;
		return new Vector3[]
		{
			center + new Vector3(-extents.x, -extents.y, -extents.z),
			center + new Vector3(extents.x, -extents.y, -extents.z),
			center + new Vector3(-extents.x, extents.y, -extents.z),
			center + new Vector3(extents.x, extents.y, -extents.z),
			center + new Vector3(-extents.x, -extents.y, extents.z),
			center + new Vector3(extents.x, -extents.y, extents.z),
			center + new Vector3(-extents.x, extents.y, extents.z),
			center + new Vector3(extents.x, extents.y, extents.z)
		};

    }

	private bool IsBoundsOutsideSafeArea(Bounds bounds)
    {
        Vector3[] corners = GetBoundsCorners(bounds);

        foreach (Vector3 corner in corners)
        {
            Vector3 vp = cam.WorldToViewportPoint(corner);

			if (vp.z < 0f) return true;
            if (vp.x < safeLeft || vp.x > safeRight ||
				vp.y < safeBottom || vp.y > safeTop)
            {
                return true;
            }
        }
        return false;
    }

}

using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    [Header("--- Target & Offset ---")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 1.5f, -10f);

    [Header("--- Smooth Settings ---")]
    [SerializeField] private float smoothSpeed = 0.15f;
    [SerializeField] private Vector2 lookAheadDistance = new Vector2(1.5f, 0f);

    [Header("--- Boundaries (Tùy chọn) ---")]
    [SerializeField] private bool useBounds = false;
    [SerializeField] private Vector2 minBounds;
    [SerializeField] private Vector2 maxBounds;

    private Vector3 currentVelocity;

    private void LateUpdate()
    {
        if (target == null)
        {
            // Tự động tìm Player nếu chưa gắn target
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) target = playerObj.transform;
            else return;
        }

        Vector3 targetPosition = target.position + offset;

        if (useBounds)
        {
            targetPosition.x = Mathf.Clamp(targetPosition.x, minBounds.x, maxBounds.x);
            targetPosition.y = Mathf.Clamp(targetPosition.y, minBounds.y, maxBounds.y);
        }

        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, smoothSpeed);
    }
}

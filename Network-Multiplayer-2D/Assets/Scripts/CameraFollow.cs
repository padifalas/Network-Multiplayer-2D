using Unity.Netcode;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Follow Settings")]
    [SerializeField] private float smoothSpeed = 8f;
    [SerializeField] private Vector3 offset = new Vector3(0f, 1f, -10f);

    [Header("Bounds (optional)")]
    [SerializeField] private bool useBounds = true;
    [SerializeField] private float minX = -10f;
    [SerializeField] private float maxX = 10f;
    [SerializeField] private float minY = -5f;
    [SerializeField] private float maxY = 5f;

    private Transform target;

    private void LateUpdate()
    {
        if (target == null)
        {
            FindLocalPlayer();
            return;
        }

        Vector3 desired = target.position + offset;

        if (useBounds)
        {
            desired.x = Mathf.Clamp(desired.x, minX, maxX);
            desired.y = Mathf.Clamp(desired.y, minY, maxY);
        }

  
        desired.z = offset.z;

        transform.position = Vector3.Lerp(
            transform.position, desired, Time.deltaTime * smoothSpeed);
    }

    private void FindLocalPlayer()
    {
        foreach (var player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (player.IsOwner)
            {
                target = player.transform;
                return;
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!useBounds) return;

        Gizmos.color = Color.yellow;

   
        Vector3 topLeft = new Vector3(minX, maxY, offset.z);
        Vector3 topRight = new Vector3(maxX, maxY, offset.z);
        Vector3 bottomLeft = new Vector3(minX, minY, offset.z);
        Vector3 bottomRight = new Vector3(maxX, minY, offset.z);

        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(bottomRight, bottomLeft);
        Gizmos.DrawLine(bottomLeft, topLeft);
    }
}

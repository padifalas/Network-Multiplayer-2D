using UnityEngine;

public class GunPickupBob : MonoBehaviour
{
    [Header("Bob Settings")]
    [SerializeField] private float bobHeight = 0.15f;
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float rotateSpeed = 0f;

    private Vector3 originPosition;

    private void Start()
    {
        originPosition = transform.localPosition;
    }

    private void Update()
    {
        
        float newY = originPosition.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.localPosition = new Vector3(originPosition.x, newY, originPosition.z);

       
        transform.Rotate(0f, 0f, rotateSpeed * Time.deltaTime);
    }
}
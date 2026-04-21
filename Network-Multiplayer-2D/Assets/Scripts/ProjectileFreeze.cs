using Unity.Netcode;
using UnityEngine;

public class ProjectileFreeze : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 12f;
    [SerializeField] private float lifetime  = 3f;

    private Vector2 direction;
    private float spawnTime;

    public void SetDirection(Vector2 dir)
    {
        direction = dir.normalized;
        spawnTime = Time.time;
    }

    private void Update()
    {
        if (!IsServer) return;

        transform.Translate(direction * moveSpeed * Time.deltaTime);

        if (Time.time - spawnTime > lifetime)
            GetComponent<NetworkObject>().Despawn();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        player.ApplyFreeze();
        GetComponent<NetworkObject>().Despawn();
    }
}
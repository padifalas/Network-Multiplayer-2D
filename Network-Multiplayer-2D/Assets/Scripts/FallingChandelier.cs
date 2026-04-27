using UnityEngine;
using Unity.Netcode;

public class FallingChandelier : NetworkBehaviour
{
    private Rigidbody2D rb;

    void Start()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();

        rb.simulated = false;

    }
    void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsServer) return;

        if (collision.CompareTag("Player"))
        {
            rb.simulated = true;
        }
    }
}

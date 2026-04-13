using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpForce = 14f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckRadius = 0.15f;

    [Header("Visuals")]
    [SerializeField] private Sprite player1Sprite;
    [SerializeField] private Sprite player2Sprite;
    [SerializeField] private Color player1Color = Color.red;
    [SerializeField] private Color player2Color = Color.orange;

 
    public NetworkVariable<bool> ControlsFlipped = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    
    private PlayerInputActions input;
    private Rigidbody2D  rb;
    private SpriteRenderer sr;
    private Vector2  moveInput;
    private bool isGrounded;
    private bool jumpQueued;
    private Vector3 spawnPoint;



    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        spawnPoint = transform.position;

    
        bool isPlayerOne = OwnerClientId == 0;
        sr.sprite = isPlayerOne ? player1Sprite : player2Sprite;
        sr.color  = isPlayerOne ? player1Color  : player2Color;

 
        if (!IsOwner) return;

        input = new PlayerInputActions();
        input.Player.Enable();
        input.Player.Jump.performed += _ => jumpQueued = true;
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner) input?.Dispose();
    }


    private void Update()
    {
        if (!IsOwner) return;
        moveInput = input.Player.Move.ReadValue<Vector2>();
        isGrounded = Physics2D.OverlapCircle (groundCheck.position, groundCheckRadius, groundLayer);
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;

   
        float direction = ControlsFlipped.Value ? -1f : 1f;
        float horizontal = moveInput.x * direction * moveSpeed;

        rb.linearVelocity = new Vector2(horizontal, rb.linearVelocity.y);

       
        if (moveInput.x != 0)
            sr.flipX = horizontal < 0;

        // Jump
        if (jumpQueued && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpQueued = false;
        }
        else
        {
            jumpQueued = false; 
        }
    }




    public void FlipControls(bool flipped)
    {
        if (!IsServer) return;
        ControlsFlipped.Value = flipped;
    }


public void SetSpawnPoint(Vector3 point)
{
    if (!IsServer) return;
    spawnPoint         = point;
    transform.position = point;

   
    SetSpawnPointClientRpc(point);
}

[ClientRpc]
private void SetSpawnPointClientRpc(Vector3 point)
{
    spawnPoint = point;
}

    public void Die()
{
    if (!IsOwner) return;
    RespawnServerRpc();
}

[ServerRpc]
private void RespawnServerRpc()
{
    transform.position = spawnPoint;
    rb.linearVelocity  = Vector2.zero;
}
}
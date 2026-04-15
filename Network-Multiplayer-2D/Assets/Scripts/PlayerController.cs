using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

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

    [Header("Sabotage")]
    [SerializeField] private float freezeDuration  = 1f;
    [SerializeField] private float frozenSpeedMult = 0.2f;
    [SerializeField] private Color frozenColor = new Color(0.7f, 0.9f, 1f); 
    [SerializeField] private ParticleSystem freezeParticles;
    [SerializeField] private GameObject gunVisual;   
    [SerializeField] private NetworkObject projectilePrefab;
    [SerializeField] private Transform gunHand; // drag GunPivot in here


    public NetworkVariable<bool> ControlsFlipped = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> IsFrozen = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);


    private PlayerInputActions input;
    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private Vector2  moveInput;
    private bool isGrounded;
    private bool jumpQueued;
    private Vector3 spawnPoint;

    private bool hasGun;
    private bool isFrozen;



    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        spawnPoint = transform.position;


        bool isPlayerOne = OwnerClientId == 0;
        sr.sprite = isPlayerOne ? player1Sprite : player2Sprite;
        sr.color  = isPlayerOne ? player1Color  : player2Color;

        IsFrozen.OnValueChanged += OnFrozenChanged;

        if (!IsOwner) return;

        input = new PlayerInputActions();
        input.Player.Enable();
        input.Player.Jump.performed  += _ => jumpQueued = true;
        input.Player.Shoot.performed += _ => TryShoot();
    }

    public override void OnNetworkDespawn()
    {
        IsFrozen.OnValueChanged -= OnFrozenChanged;
        if (IsOwner) input?.Dispose();
    }



    private void Update()
    {
        if (!IsOwner) return;
        moveInput  = input.Player.Move.ReadValue<Vector2>();
        isGrounded= Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;


        float speedMultiplier = IsFrozen.Value ? frozenSpeedMult : 1f;
        float direction = ControlsFlipped.Value ? -1f : 1f;
        float horizontal = moveInput.x * direction * moveSpeed * speedMultiplier;

        rb.linearVelocity = new Vector2(horizontal, rb.linearVelocity.y);


        if (moveInput.x != 0)
            sr.flipX = horizontal < 0;

            
    if (gunHand != null)
    {
        Vector3 s = gunHand.localScale;
        gunHand.localScale = new Vector3(
            horizontal < 0 ? -Mathf.Abs(s.x) : Mathf.Abs(s.x), s.y, s.z);

        // jump
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
    }




    public void FlipControls(bool flipped)
    {
        if (!IsServer) return;
        ControlsFlipped.Value = flipped;
    }

    public void EquipGun()
    {
        if (!IsServer) return;
        EquipGunClientRpc();
    }

    [ClientRpc]
    private void EquipGunClientRpc()
    {
        if (!IsOwner) return;
        hasGun = true;
        if (gunVisual != null) gunVisual.SetActive(true);
    }

    private void TryShoot()
    {
        if (!IsOwner) return;
        if (!hasGun)  return;
        ShootServerRpc(sr.flipX ? Vector2.left : Vector2.right);
    }

    [ServerRpc]
    private void ShootServerRpc(Vector2 direction)
    {
        if (!hasGun) return;
        hasGun = false;

        NetworkObject projectile = Instantiate(projectilePrefab, transform.position + (Vector3)(direction * 0.8f),Quaternion.identity);

        projectile.Spawn();
        projectile.GetComponent<ProjectileFreeze>().SetDirection(direction);

        DisarmClientRpc();
    }

    [ClientRpc]
    private void DisarmClientRpc()
    {
        hasGun = false;
        if (gunVisual != null) gunVisual.SetActive(false);
    }

    public void ApplyFreeze()
    {
        if (!IsServer) return;
        StartCoroutine(FreezeRoutine());
    }

    private IEnumerator FreezeRoutine()
    {
        IsFrozen.Value = true;
        yield return new WaitForSeconds(freezeDuration);
        IsFrozen.Value = false;
    }

    private void OnFrozenChanged(bool previous, bool current)
    {
        sr.color = current ? frozenColor : (OwnerClientId == 0 ? player1Color : player2Color);

        if (freezeParticles == null) return;
        if (current  && !freezeParticles.isPlaying) freezeParticles.Play();
        if (!current &&  freezeParticles.isPlaying) freezeParticles.Stop();
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
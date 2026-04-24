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
    [SerializeField] private Transform player1SpawnPoint;
    [SerializeField] private Transform player2SpawnPoint;

    [Header("Sabotage")]
    [SerializeField] private float freezeDuration  = 1f;
    [SerializeField] private float frozenSpeedMult = 0.2f;
    [SerializeField] private Color frozenColor = new Color(0.7f, 0.9f, 1f);
    [SerializeField] private ParticleSystem freezeParticles;
    [SerializeField] private GameObject gunVisual;
    [SerializeField] private NetworkObject projectilePrefab;
    [SerializeField] private Transform gunHand;

    public NetworkVariable<bool> ControlsFlipped = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> IsFrozen        = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private PlayerInputActions input;
    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private Vector2 moveInput;
    private bool isGrounded;
    private bool jumpQueued;
    private Vector3 spawnPoint;
    private bool hasGun;

    // ──────────────────────────────────────────────────────────────────────────
    //  Awake – find spawn points by name if not assigned in the Inspector
    // ──────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (player1SpawnPoint == null)
            player1SpawnPoint = FindSceneSpawnPoint(
                "Player1SpawnPoint", "Player1Spawn", "P1SpawnPoint", "P1-SpawnPoint", "P1Spawn");

        if (player2SpawnPoint == null)
            player2SpawnPoint = FindSceneSpawnPoint(
                "Player2SpawnPoint", "Player2Spawn", "P2SpawnPoint", "P2Spawn");
    }

    private Transform FindSceneSpawnPoint(params string[] names)
    {
        foreach (var name in names)
        {
            if (string.IsNullOrEmpty(name)) continue;
            var obj = GameObject.Find(name);
            if (obj != null) return obj.transform;
        }
        return null;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  OnNetworkSpawn
    // ──────────────────────────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();

        if (rb == null) Debug.LogError($"[PlayerController] Rigidbody2D missing on {name}", this);
        if (sr == null) Debug.LogError($"[PlayerController] SpriteRenderer missing on {name}", this);

        bool isPlayerOne = OwnerClientId == 0;
        Transform selectedSpawn = isPlayerOne ? player1SpawnPoint : player2SpawnPoint;

        if (selectedSpawn != null)
        {
            spawnPoint = selectedSpawn.position;
            transform.position = spawnPoint;
        }
        else
        {
            Debug.LogWarning($"[PlayerController] No spawn point for {(isPlayerOne ? "P1" : "P2")} – using current position.", this);
            spawnPoint = transform.position;
        }

        sr.sprite = isPlayerOne ? player1Sprite : player2Sprite;
        sr.color  = isPlayerOne ? player1Color  : player2Color;

        IsFrozen.OnValueChanged += OnFrozenChanged;

        // Only the owning client sets up input.
        if (!IsOwner) return;

        SetupInput();
    }

    public override void OnNetworkDespawn()
    {
        IsFrozen.OnValueChanged -= OnFrozenChanged;
        if (IsOwner) input?.Dispose();
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Input setup – NO device filter; either player uses any keyboard/gamepad
    // ──────────────────────────────────────────────────────────────────────────
    private void SetupInput()
    {
        input = new PlayerInputActions();

        // Clear any device restrictions baked into the asset so both players
        // can freely use any connected keyboard or gamepad.
        input.devices = null;

        input.Player.Enable();

        input.Player.Jump.performed  += _ => jumpQueued = true;
        input.Player.Shoot.performed += _ => TryShoot();

        Debug.Log($"[PlayerController] Input ready for OwnerClientId={OwnerClientId}. " +
                  $"Gamepads={Gamepad.all.Count}, Keyboards detected via InputSystem.");
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Update / FixedUpdate
    // ──────────────────────────────────────────────────────────────────────────
    private void Update()
    {
        if (!IsOwner) return;

        moveInput  = input.Player.Move.ReadValue<Vector2>();
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        float speedMultiplier = IsFrozen.Value ? frozenSpeedMult : 1f;
        float direction       = ControlsFlipped.Value ? -1f : 1f;
        float horizontal      = moveInput.x * direction * moveSpeed * speedMultiplier;

        rb.linearVelocity = new Vector2(horizontal, rb.linearVelocity.y);

        // Flip sprite
        if (moveInput.x != 0)
            sr.flipX = horizontal < 0;

        // Flip gun hand to match facing direction
        if (gunHand != null)
        {
            Vector3 s = gunHand.localScale;
            gunHand.localScale = new Vector3(
                horizontal < 0 ? -Mathf.Abs(s.x) : Mathf.Abs(s.x), s.y, s.z);
        }

        // Jump
        if (jumpQueued && isGrounded)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);

        jumpQueued = false;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Sabotage – Flip Controls
    // ──────────────────────────────────────────────────────────────────────────
    public void FlipControls(bool flipped)
    {
        if (!IsServer) return;
        ControlsFlipped.Value = flipped;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Sabotage – Gun / Shoot
    // ──────────────────────────────────────────────────────────────────────────
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

        NetworkObject projectile = Instantiate(
            projectilePrefab,
            transform.position + (Vector3)(direction * 0.8f),
            Quaternion.identity);

        projectile.Spawn();
        projectile.GetComponent<ProjectileFreeze>().SetDirection(direction);

        DisarmClientRpc();
        AudioManager.Singleton?.PlayShoot();
    }

    [ClientRpc]
    private void DisarmClientRpc()
    {
        hasGun = false;
        if (gunVisual != null) gunVisual.SetActive(false);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Sabotage – Freeze
    // ──────────────────────────────────────────────────────────────────────────
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
        AudioManager.Singleton?.PlayFreeze();
    }

    private void OnFrozenChanged(bool previous, bool current)
    {
        sr.color = current ? frozenColor : (OwnerClientId == 0 ? player1Color : player2Color);

        if (freezeParticles == null) return;
        if ( current && !freezeParticles.isPlaying) freezeParticles.Play();
        if (!current &&  freezeParticles.isPlaying) freezeParticles.Stop();
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Knockback
    // ──────────────────────────────────────────────────────────────────────────
    public void ApplyKnockback(Vector2 direction, float force)
    {
        if (!IsServer) return;
        ApplyKnockbackClientRpc(direction, force);
        AudioManager.Singleton?.PlayKnockback();
    }

    [ClientRpc]
    private void ApplyKnockbackClientRpc(Vector2 direction, float force)
    {
        if (!IsOwner) return;
        rb.linearVelocity = new Vector2(direction.x * force, force * 0.5f);
        StartCoroutine(CameraShakeRoutine());
        // Note: audio already fired server-side; skip duplicate call here.
    }

    private IEnumerator CameraShakeRoutine()
    {
        Camera cam         = Camera.main;
        Vector3 originPos  = cam.transform.localPosition;
        float elapsed      = 0f;
        const float duration  = 0.3f;
        const float magnitude = 0.15f;

        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;
            cam.transform.localPosition = new Vector3(originPos.x + x, originPos.y + y, originPos.z);
            elapsed += Time.deltaTime;
            yield return null;
        }

        cam.transform.localPosition = originPos;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Spawn point
    // ──────────────────────────────────────────────────────────────────────────
    public void SetSpawnPoint(Vector3 point)
    {
        if (!IsServer) return;
        spawnPoint = point;
        transform.position = point;
        SetSpawnPointClientRpc(point);
    }

    [ClientRpc]
    private void SetSpawnPointClientRpc(Vector3 point)
    {
        spawnPoint = point;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Death / Respawn
    // ──────────────────────────────────────────────────────────────────────────
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
        AudioManager.Singleton?.PlayDeath();
        RespawnClientRpc(spawnPoint);
    }

    // Ensures the owning client's rigidbody is snapped to the correct position
    // and velocity is cleared so there's no desync after respawn.
    [ClientRpc]
    private void RespawnClientRpc(Vector3 position)
    {
        if (!IsOwner) return;
        transform.position = position;
        rb.linearVelocity  = Vector2.zero;
    }
}
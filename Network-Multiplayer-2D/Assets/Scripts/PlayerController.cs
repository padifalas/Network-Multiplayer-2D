using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed       = 6f;
    [SerializeField] private float jumpForce       = 14f;
    [SerializeField] private float acceleration    = 12f;
    [SerializeField] private float deceleration    = 18f;
    [SerializeField] private float airAcceleration = 6f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckRadius = 0.15f;

    [Header("Visuals")]
    [SerializeField] private GameObject player1Visual;
    [SerializeField] private GameObject player2Visual;
    [SerializeField] private Color      player1Color = Color.red;
    [SerializeField] private Color      player2Color = Color.cyan;
    [SerializeField] private Transform  player1SpawnPoint;
    [SerializeField] private Transform  player2SpawnPoint;

    [Header("Sabotage")]
    [SerializeField] private float          freezeDuration  = 1f;
    [SerializeField] private float          frozenSpeedMult = 0.2f;
    [SerializeField] private Color          frozenColor     = new Color(0.7f, 0.9f, 1f);
    [SerializeField] private ParticleSystem freezeParticles;
    [SerializeField] private GameObject     gunVisual;
    [SerializeField] private NetworkObject  projectilePrefab;
    [SerializeField] private Transform      gunHand;

    public NetworkVariable<bool> ControlsFlipped = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> IsFrozen        = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> HasGun          = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private PlayerInputActions input;
    private Rigidbody2D        rb;
    private Animator           animator;
    private SpriteRenderer[]   allRenderers;
    private Vector2            moveInput;
    private bool               isGrounded;
    private bool               jumpQueued;
    private bool               crouchInput;
    private Vector3            spawnPoint;
    private bool               facingLeft;
    private Color              myColor;

    private static readonly int HashSpeed  = Animator.StringToHash("Speed");
    private static readonly int HashGround = Animator.StringToHash("Grounded");
    private static readonly int HashJump   = Animator.StringToHash("Jumping");
    private static readonly int HashCrouch = Animator.StringToHash("Crouching");
    private static readonly int HashHasGun = Animator.StringToHash("HasGun");
    private static readonly int HashShoot  = Animator.StringToHash("Shoot");


    private void Awake()
    {
        if (player1SpawnPoint == null)
            player1SpawnPoint = FindSceneSpawnPoint("Player1SpawnPoint", "Player1Spawn", "P1SpawnPoint", "P1-SpawnPoint", "P1Spawn");

        if (player2SpawnPoint == null)
            player2SpawnPoint = FindSceneSpawnPoint("Player2SpawnPoint", "Player2Spawn", "P2SpawnPoint", "P2Spawn");
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


    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody2D>();

        if (rb == null) Debug.LogError($"[PlayerController] Rigidbody2D missing on {name}", this);

        bool isPlayerOne = OwnerClientId == 0;

        // activate the correct visual rig for this player
        if (player1Visual != null) player1Visual.SetActive(isPlayerOne);
        if (player2Visual != null) player2Visual.SetActive(!isPlayerOne);

        allRenderers = GetComponentsInChildren<SpriteRenderer>();
        animator     = GetComponentInChildren<Animator>();

        myColor = isPlayerOne ? player1Color : player2Color;
        ApplyColor(myColor);

        // spawn position
        Transform selectedSpawn = isPlayerOne ? player1SpawnPoint : player2SpawnPoint;
        if (selectedSpawn != null)
        {
            spawnPoint         = selectedSpawn.position;
            transform.position = spawnPoint;
        }
        else
        {
            spawnPoint = transform.position;
        }

        IsFrozen.OnValueChanged += OnFrozenChanged;
        HasGun.OnValueChanged   += OnHasGunChanged;

        if (!IsOwner) return;

        SetupInput();
    }

    public override void OnNetworkDespawn()
    {
        IsFrozen.OnValueChanged -= OnFrozenChanged;
        HasGun.OnValueChanged   -= OnHasGunChanged;
        if (IsOwner) input?.Dispose();
    }


    // input setup

    private void SetupInput()
    {
        input         = new PlayerInputActions();
        input.devices = null;
        input.Player.Enable();
        input.Player.Jump.performed  += _ => jumpQueued = true;
        input.Player.Shoot.performed += _ => TryShoot();
    }


    private void Update()
    {
        if (!IsOwner) return;

        moveInput   = input.Player.Move.ReadValue<Vector2>();
        isGrounded  = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        crouchInput = moveInput.y < -0.5f;

        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        // crouching — brake to a stop
        if (crouchInput)
        {
            float braked      = Mathf.MoveTowards(rb.linearVelocity.x, 0f, deceleration * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector2(braked, rb.linearVelocity.y);
            jumpQueued        = false;
            return;
        }

        float speedMultiplier = IsFrozen.Value ? frozenSpeedMult : 1f;
        float directionMult   = ControlsFlipped.Value ? -1f : 1f;
        float targetSpeed     = moveInput.x * directionMult * moveSpeed * speedMultiplier;
        float currentSpeed    = rb.linearVelocity.x;

        float accelRate = isGrounded
            ? (Mathf.Abs(targetSpeed) > 0.01f ? acceleration    : deceleration)
            : (Mathf.Abs(targetSpeed) > 0.01f ? airAcceleration : deceleration * 0.5f);

        float newHorizontal   = Mathf.MoveTowards(currentSpeed, targetSpeed, accelRate * Time.fixedDeltaTime);
        rb.linearVelocity     = new Vector2(newHorizontal, rb.linearVelocity.y);

        // flip entire rig via root scale
        if (moveInput.x != 0)
        {
            facingLeft           = newHorizontal < 0;
            Vector3 s            = transform.localScale;
            s.x                  = Mathf.Abs(s.x) * (facingLeft ? -1f : 1f);
            transform.localScale = s;
        }

        // gun hand flip
        if (gunHand != null)
        {
            Vector3 s          = gunHand.localScale;
            gunHand.localScale = new Vector3(
                facingLeft ? -Mathf.Abs(s.x) : Mathf.Abs(s.x), s.y, s.z);
        }

        // Jump
        if (jumpQueued && isGrounded)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);

        jumpQueued = false;
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        animator.SetFloat(HashSpeed,  Mathf.Abs(rb.linearVelocity.x));
        animator.SetBool(HashGround,  isGrounded);
        animator.SetBool(HashJump,    rb.linearVelocity.y > 0.1f && !isGrounded);
        animator.SetBool(HashCrouch,  crouchInput && isGrounded);
        animator.SetBool(HashHasGun,  HasGun.Value);
    }


    public void FlipControls(bool flipped)
    {
        if (!IsServer) return;
        ControlsFlipped.Value = flipped;
    }

    // server sets HasGun — NetworkVariable syncs visuals to all clients via OnHasGunChanged
    public void EquipGun()
    {
        if (!IsServer) return;
        HasGun.Value = true;
    }

    private void OnHasGunChanged(bool previous, bool current)
    {
        if (gunVisual != null) gunVisual.SetActive(current);
        if (animator  != null) animator.SetBool(HashHasGun, current);
    }

    private void TryShoot()
    {
        if (!IsOwner)      return;
        if (!HasGun.Value) return;
        ShootServerRpc(facingLeft ? Vector2.left : Vector2.right);
        TriggerShootAnimClientRpc();
    }

    [ServerRpc]
    private void ShootServerRpc(Vector2 direction)
    {
        if (!HasGun.Value) return;

        HasGun.Value = false;

        NetworkObject projectile = Instantiate(
            projectilePrefab,
            transform.position + (Vector3)(direction * 0.8f),
            Quaternion.identity);

        projectile.Spawn();
        projectile.GetComponent<ProjectileFreeze>().SetDirection(direction);
        AudioManager.Singleton?.PlayShoot();
    }

    // trigger shoot animation on the shooting player's client only
    [ClientRpc]
    private void TriggerShootAnimClientRpc()
    {
        if (!IsOwner) return;
        if (animator != null) animator.SetTrigger(HashShoot);
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
        AudioManager.Singleton?.PlayFreeze();
    }

    private void OnFrozenChanged(bool previous, bool current)
    {
        ApplyColor(current ? frozenColor : myColor);

        if (freezeParticles == null) return;
        if ( current && !freezeParticles.isPlaying) freezeParticles.Play();
        if (!current &&  freezeParticles.isPlaying) freezeParticles.Stop();
    }

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
    }

    private IEnumerator CameraShakeRoutine()
    {
        Camera  cam       = Camera.main;
        Vector3 originPos = cam.transform.localPosition;
        float   elapsed   = 0f;

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


    // Respawn

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

    [ClientRpc]
    private void RespawnClientRpc(Vector3 position)
    {
        if (!IsOwner) return;
        transform.position = position;
        rb.linearVelocity  = Vector2.zero;
    }


    // helpers

    private void ApplyColor(Color color)
    {
        if (allRenderers == null) return;
        foreach (SpriteRenderer r in allRenderers)
        {
            // skip gun hand children so gun keeps its own colors
            if (gunHand != null && r.transform.IsChildOf(gunHand)) continue;
            r.color = color;
        }
    }
}
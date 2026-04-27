using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpForce = 14f;
    [SerializeField] private float acceleration  = 12f;   // how fast player reaches full speed
    [SerializeField] private float deceleration = 18f;   // how fast player stops
    [SerializeField] private float airAcceleration= 6f;    // less control in air

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
    public NetworkVariable<bool> IsFrozen= new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private PlayerInputActions input;
    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private Vector2 moveInput;
    private bool isGrounded;
    private bool jumpQueued;
    private Vector3 spawnPoint;
    private bool hasGun;


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
            spawnPoint = transform.position;
        }

        sr.sprite = isPlayerOne ? player1Sprite : player2Sprite;
        sr.color  = isPlayerOne ? player1Color  : player2Color;

        IsFrozen.OnValueChanged += OnFrozenChanged;

        if (!IsOwner) return;

        SetupInput();
    }

    public override void OnNetworkDespawn()
    {
        IsFrozen.OnValueChanged -= OnFrozenChanged;
        if (IsOwner) input?.Dispose();
    }


    // input setup

    private void SetupInput()
    {
        input          = new PlayerInputActions();
        input.devices  = null;
        input.Player.Enable();
        input.Player.Jump.performed  += _ => jumpQueued = true;
        input.Player.Shoot.performed += _ => TryShoot();
    }


    private void Update()
    {
        if (!IsOwner) return;

        moveInput  = input.Player.Move.ReadValue<Vector2>();
        isGrounded = Physics2D.OverlapCircle(
            groundCheck.position, groundCheckRadius, groundLayer);
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        float speedMultiplier = IsFrozen.Value ? frozenSpeedMult : 1f;
        float directionMult = ControlsFlipped.Value ? -1f : 1f;
        float targetSpeed  = moveInput.x * directionMult * moveSpeed * speedMultiplier;

        
        float currentSpeed = rb.linearVelocity.x;
        float accelRate;

        if (isGrounded)
            accelRate = Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration;
        else
            accelRate = Mathf.Abs(targetSpeed) > 0.01f ? airAcceleration : deceleration * 0.5f;

        
        float newHorizontal = Mathf.MoveTowards(
            currentSpeed, targetSpeed, accelRate * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector2(newHorizontal, rb.linearVelocity.y);

        // sprite flip
        if (moveInput.x != 0) sr.flipX = newHorizontal < 0;

        // gun hand flip
        if (gunHand != null)
        {
            Vector3 s          = gunHand.localScale;
            gunHand.localScale = new Vector3(
                sr.flipX ? -Mathf.Abs(s.x) : Mathf.Abs(s.x), s.y, s.z);
        }

        // jump stuff
        if (jumpQueued && isGrounded)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);

        jumpQueued = false;
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
            cam.transform.localPosition = new Vector3(
                originPos.x + x, originPos.y + y, originPos.z);
            elapsed += Time.deltaTime;
            yield return null;
        }

        cam.transform.localPosition = originPos;
    }

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


    // respawn

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
}
using Unity.Netcode;
using UnityEngine;
using System.Collections;


public class Obstacle : NetworkBehaviour
{
    public enum ObstacleType { Fire, Sink, Chandelier, TeleportingWall, Killbox, Bookshelf }

    [Header("Type")]
    [SerializeField] private ObstacleType obstacleType;

    [Header("Fire Settings")]
    [SerializeField] private float fireExpandScale = 2.5f;
    [SerializeField] private float fireExpandSpeed = 3f;
    [SerializeField] private float fireDetectRadius = 3f;

    [Header("Sink Settings")]
    [SerializeField] private float sinkSpeed = 2f;
    [SerializeField] private float sinkDetectRadius = 2f;
    [SerializeField] private float sinkResetDelay = 2f;
    [SerializeField] private Transform killboxTransform;

    [Header("Chandelier Settings")]
[SerializeField] private Rigidbody2D chandelierRb;
    private bool chandelierDropped;
    [SerializeField] private float chandelierShakeDuration  = 0.4f;
    [SerializeField] private float chandelierShakeMagnitude = 0.25f;

    [Header("Pushing Wall")]
    [SerializeField] private float wallMoveSpeed = 8f;
    [SerializeField] private float wallChargeDelay = 0.3f;

    [Header("Bookshelf Settings")]
    [SerializeField] private Transform bookSpawnPoint;
    [SerializeField] private NetworkObject bookProjectilePrefab;

    [Header("Particles")]
    [SerializeField] private ParticleSystem obstacleParticles;

    private Vector3 originPosition;
    private Vector3 originScale;

    // fire
    private bool isExpanded;

    // sink
    private bool isSinking;
    private float sinkTargetY;
    private Coroutine sinkResetCoroutine;

    // wall
    private bool isCharging;
    private bool wallUsed;
    private Transform wallTarget;

    private void Start()
    {
        originPosition = transform.position;
        originScale = transform.localScale;

if (obstacleType == ObstacleType.Chandelier && chandelierRb != null)
{
    chandelierRb.bodyType = RigidbodyType2D.Static;
}
       

        if (obstacleType == ObstacleType.TeleportingWall)
            SetWallVisible(false);

        if (obstacleType == ObstacleType.Sink && killboxTransform != null)
            sinkTargetY = killboxTransform.position.y - 1.5f;
        else
            sinkTargetY = originPosition.y - 3f;
    }

    private void Update()
    {
        if (!IsServer) return;

        switch (obstacleType)
        {
            case ObstacleType.Fire: HandleFire(); break;
            case ObstacleType.Sink: HandleSink(); break;
            case ObstacleType.TeleportingWall: HandleWall(); break;

        }
    }

    // fire obs

    private void HandleFire()
    {
        PlayerController nearest = GetNearestPlayer(fireDetectRadius);

        if (nearest != null)
        {
            transform.localScale = Vector3.Lerp(
                transform.localScale,
                originScale * fireExpandScale,
                Time.deltaTime * fireExpandSpeed);

            SyncParticlesClientRpc(true);
        }
        else
        {
            transform.localScale = Vector3.Lerp(
                transform.localScale,
                originScale,
                Time.deltaTime * fireExpandSpeed);

            SyncParticlesClientRpc(false);
        }
    }



    private void HandleSink()
    {
        PlayerController nearest = GetNearestPlayer(sinkDetectRadius);

        if (nearest != null && !isSinking)
        {
            isSinking = true;
            if (sinkResetCoroutine != null) StopCoroutine(sinkResetCoroutine);
            sinkResetCoroutine = StartCoroutine(SinkRoutine());
        }
    }

private void HandleFallingChandelier()
{
    if (!IsServer) return;
    if (chandelierDropped) return;

    chandelierDropped = true;

    // Switch rigidbody to dynamic so it falls
    if (chandelierRb != null)
        chandelierRb.bodyType = RigidbodyType2D.Dynamic;

    // Tell clients to also apply shake
    HandleFallingChandelierClientRpc();
}

[ClientRpc]
private void HandleFallingChandelierClientRpc()
{
    if (chandelierRb != null)
        chandelierRb.bodyType = RigidbodyType2D.Dynamic;

    // Trigger screen shake effect
    StartCoroutine(ChandelierScreenShake());
}

private IEnumerator ChandelierScreenShake()
{
    Camera cam  = Camera.main;
    Vector3 originPos = cam.transform.localPosition;
    float  elapsed  = 0f;

    while (elapsed < chandelierShakeDuration)
    {
        float x = Random.Range(-1f, 1f) * chandelierShakeMagnitude;
        float y = Random.Range(-1f, 1f) * chandelierShakeMagnitude;

        cam.transform.localPosition = new Vector3(originPos.x + x,originPos.y + y,originPos.z);

        elapsed += Time.deltaTime;
        yield return null;
    }

    cam.transform.localPosition = originPos;
}


    private IEnumerator SinkRoutine()
    {
        // go down until platform is below the killbox
        while (transform.position.y > sinkTargetY)
        {
            transform.position += Vector3.down * sinkSpeed * Time.deltaTime;
            SyncSinkPositionClientRpc(transform.position);
            yield return null;
        }

        transform.position = new Vector3(originPosition.x, sinkTargetY, originPosition.z);
        SyncSinkPositionClientRpc(transform.position);

        yield return new WaitForSeconds(sinkResetDelay);

        while (Vector3.Distance(transform.position, originPosition) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, originPosition, sinkSpeed * Time.deltaTime);
            SyncSinkPositionClientRpc(transform.position);
            yield return null;
        }

        transform.position = originPosition;
        SyncSinkPositionClientRpc(transform.position);
        isSinking = false;
    }


    // pushing wall obstacle

    private void HandleWall()
    {
        if (!isCharging || wallTarget == null) return;

        Vector3 target = new Vector3(wallTarget.position.x, originPosition.y, 0f);

        transform.position = Vector3.MoveTowards(
            transform.position,
            target,
            wallMoveSpeed * Time.deltaTime);

        SyncWallPositionClientRpc(transform.position);

        if (Vector3.Distance(transform.position, target) < 0.3f)
        {
            isCharging = false;
            wallTarget = null;
            StartCoroutine(ResetWallRoutine());
        }
    }

    private IEnumerator ResetWallRoutine()
    {
        yield return new WaitForSeconds(1f);
        SetWallVisibleClientRpc(false);
        transform.position = originPosition;
        wallUsed = false;
    }


    // bookshelf obstacle
    // fires a book toward the player only when they enter the trigger
    // direction is determined by which side of the bookshelf the player enters from

    private void FireBook(Vector2 direction)
    {
        if (bookProjectilePrefab == null) return;

        Vector3 spawnPos = bookSpawnPoint != null
            ? bookSpawnPoint.position
            : transform.position;

        NetworkObject book = Instantiate(
            bookProjectilePrefab, spawnPos, Quaternion.identity);

        book.Spawn();
        book.GetComponent<BookProjectile>().SetDirection(direction);
    }


    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        switch (obstacleType)
        {
            case ObstacleType.Killbox:
            case ObstacleType.Fire:
                if (player.IsOwner)
                {
                    player.Die();
                    RequestDeathParticlesServerRpc(other.transform.position);
                }
                break;

             case ObstacleType.Chandelier:
             if (!IsServer) return;
          
             RequestDeathParticlesServerRpc(other.transform.position);
            
            //  Debug.Log("chandelier trigger hit — calling HandleFallingChandelier");
            HandleFallingChandelier();
             break;

            case ObstacleType.Bookshelf:
                if (!IsServer) return;
                AudioManager.Singleton?.PlayBook();


                float playerX = player.transform.position.x;
                float spawnX = bookSpawnPoint != null? bookSpawnPoint.position.x : transform.position.x;
                Vector2 fireDirection = playerX > spawnX ? Vector2.right : Vector2.left;

                FireBook(fireDirection);
                ShakeBookshelfClientRpc();
                break;

            case ObstacleType.TeleportingWall:
                if (!IsServer) return;

                if (!wallUsed && !isCharging)
                {
                    wallUsed = true;
                    wallTarget = player.transform;
                    StartCoroutine(ChargeAfterDelay());
                }
                else if (isCharging)
                {
                    KillPlayer(player, other.transform.position);
                }
                break;
        }
    }

   private void OnCollisionEnter2D(Collision2D collision)
{
    if (obstacleType != ObstacleType.Chandelier) return;
    if (!IsServer) return;

    PlayerController player = collision.collider.GetComponent<PlayerController>();

    if (player != null && player.IsOwner)
    {
    
        player.Die();
        RequestDeathParticlesServerRpc(player.transform.position);
        AudioManager.Singleton?.PlayFallingChandelier();
    }
    else
    {
       
        RequestGroundImpactParticlesServerRpc(transform.position);
        AudioManager.Singleton?.PlayFallingChandelier();
    }


    ImpactShakeClientRpc();

   
    StartCoroutine(DestroyChandelierAfterDelay());
}

[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
private void RequestGroundImpactParticlesServerRpc(Vector3 position)
{
    PlayGroundImpactParticlesClientRpc(position);
}

[ClientRpc]
private void PlayGroundImpactParticlesClientRpc(Vector3 position)
{
    if (obstacleParticles == null) return;

    // Optionally use a different particle prefab for dust/debris
    obstacleParticles.transform.position = position;
    obstacleParticles.Play();
}

[ClientRpc]
private void ImpactShakeClientRpc()
{
    StartCoroutine(ChandelierScreenShake());
}

private IEnumerator DestroyChandelierAfterDelay()
{
    yield return new WaitForSeconds(2f);

    if (IsServer)
    {
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
            netObj.Despawn();
        else
            Destroy(gameObject);
    }
}
 

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestDeathParticlesServerRpc(Vector3 position)
    {
        PlayDeathParticlesClientRpc(position);
    }

    private IEnumerator ChargeAfterDelay()
    {
        SetWallVisibleClientRpc(true);
        yield return new WaitForSeconds(wallChargeDelay);
        isCharging = true;
    }

    private void KillPlayer(PlayerController player, Vector3 position)
    {
        player.Die();
        PlayDeathParticlesClientRpc(position);
        AudioManager.Singleton?.PlayDeath();
    }


    private PlayerController GetNearestPlayer(float radius)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
        PlayerController nearest = null;
        float closest = Mathf.Infinity;

        foreach (Collider2D hit in hits)
        {
            PlayerController p = hit.GetComponent<PlayerController>();
            if (p == null) continue;

            float dist = Vector3.Distance(transform.position, p.transform.position);
            if (dist < closest) { closest = dist; nearest = p; }
        }

        return nearest;
    }

    private void SetWallVisible(bool visible)
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        Collider2D col = GetComponent<Collider2D>();
        if (sr) sr.enabled = visible;
        if (col) col.enabled = visible;
    }


    [ClientRpc]
    private void ShakeBookshelfClientRpc()
    {
        StartCoroutine(BookshelfWobble());
    }

    private IEnumerator BookshelfWobble()
    {
        float elapsed = 0f;
        float duration = 0.2f;
        float magnitude = 0.05f;

        while (elapsed < duration)
        {
            float offset = Mathf.Sin(elapsed * 40f) * magnitude;
            transform.position = new Vector3(
                originPosition.x + offset,
                originPosition.y,
                originPosition.z);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = originPosition;
    }

    [ClientRpc]
    private void SyncSinkPositionClientRpc(Vector3 position)
    {
        if (!IsServer) transform.position = position;
    }

    [ClientRpc]
    private void SetWallVisibleClientRpc(bool visible)
    {
        SetWallVisible(visible);
    }

    [ClientRpc]
    private void SyncWallPositionClientRpc(Vector3 position)
    {
        if (!IsServer) transform.position = position;
    }

    [ClientRpc]
    private void SyncParticlesClientRpc(bool playing)
    {
        if (obstacleParticles == null) return;
        if (playing && !obstacleParticles.isPlaying) obstacleParticles.Play();
        if (!playing && obstacleParticles.isPlaying) obstacleParticles.Stop();
    }

    [ClientRpc]
    private void PlayDeathParticlesClientRpc(Vector3 position)
    {
        if (obstacleParticles == null) return;
        obstacleParticles.transform.position = position;
        obstacleParticles.Play();
    }
}
using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class Obstacle : NetworkBehaviour
{
    public enum ObstacleType { Fire, Sink, TeleportingWall, Killbox, Bookshelf }

    [Header("Type")]
    [SerializeField] private ObstacleType obstacleType;

    [Header("Fire Settings")]
    [SerializeField] private float fireExpandScale  = 2.5f;
    [SerializeField] private float fireExpandSpeed  = 3f;
    [SerializeField] private float fireDetectRadius = 3f;

    [Header("Sink Settings")]
    [SerializeField] private float  sinkSpeed        = 2f;
    [SerializeField] private float sinkDetectRadius = 2f;
    [SerializeField] private float sinkResetDelay   = 2f;
    [SerializeField] private Transform killboxTransform;

    [Header("Pushing Wall")]
    [SerializeField] private float wallMoveSpeed   = 8f;
    [SerializeField] private float wallChargeDelay = 0.3f;

    [Header("Bookshelf Settings")]
    [SerializeField] private Transform bookSpawnPoint;
    [SerializeField] private NetworkObject bookProjectilePrefab;

    [Header("Particles")]
    [SerializeField] private ParticleSystem obstacleParticles;

    private Vector3  originPosition;
    private Vector3 originScale;

    // fire
    private bool isExpanded;

    // sink
    private bool isSinking;
    private float sinkTargetY;
    private Coroutine sinkResetCoroutine;

    // wall
    private bool isCharging;
    private bool  wallUsed;
    private Transform wallTarget;


    private void Start()
    {
        originPosition = transform.position;
        originScale = transform.localScale;

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
            isSinking          = true;
            if (sinkResetCoroutine != null) StopCoroutine(sinkResetCoroutine);
            sinkResetCoroutine = StartCoroutine(SinkRoutine());
        }
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
        wallUsed           = false;
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

            case ObstacleType.Bookshelf:
                if (!IsServer) return;

               
                float playerX = player.transform.position.x;
                float spawnX  = bookSpawnPoint != null
                    ? bookSpawnPoint.position.x
                    : transform.position.x;
                Vector2 fireDirection = playerX > spawnX ? Vector2.right : Vector2.left;

                FireBook(fireDirection);
                ShakeBookshelfClientRpc();
                break;

            case ObstacleType.TeleportingWall:
                if (!IsServer) return;

                if (!wallUsed && !isCharging)
                {
                    wallUsed   = true;
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

    [ServerRpc(RequireOwnership = false)]
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
        SpriteRenderer sr  = GetComponent<SpriteRenderer>();
        Collider2D col = GetComponent<Collider2D>();
        if (sr)  sr.enabled  = visible;
        if (col) col.enabled = visible;
    }


    [ClientRpc]
    private void ShakeBookshelfClientRpc()
    {
        StartCoroutine(BookshelfWobble());
    }

    private IEnumerator BookshelfWobble()
    {
        float elapsed   = 0f;
        float duration  = 0.2f;
        float magnitude = 0.05f;

        while (elapsed < duration)
        {
            float offset       = Mathf.Sin(elapsed * 40f) * magnitude;
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
        if (playing  && !obstacleParticles.isPlaying) obstacleParticles.Play();
        if (!playing &&  obstacleParticles.isPlaying) obstacleParticles.Stop();
    }

    [ClientRpc]
    private void PlayDeathParticlesClientRpc(Vector3 position)
    {
        if (obstacleParticles == null) return;
        obstacleParticles.transform.position = position;
        obstacleParticles.Play();
    }
}
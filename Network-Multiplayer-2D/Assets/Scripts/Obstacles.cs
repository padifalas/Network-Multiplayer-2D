using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class Obstacle : NetworkBehaviour
{
    public enum ObstacleType { Fire, Sink, TeleportingWall, Killbox }

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

    [Header("Pushing Wall Settings")]
    [SerializeField] private float wallMoveSpeed = 8f;
    [SerializeField] private float wallChargeDelay= 0.3f;

    [Header("Particles")]
    [SerializeField] private ParticleSystem obstacleParticles;

    private Vector3 originPosition;
    private Vector3 originScale;

    // fire
    private bool isExpanded;

    // sink
    private bool isSinking;
    private Coroutine  sinkResetCoroutine;

    // wall
    private bool isCharging;
    private bool wallUsed;
    private Transform  wallTarget;


    private void Start()
    {
        originPosition = transform.position;
        originScale = transform.localScale;

        
        if (obstacleType == ObstacleType.TeleportingWall)
            SetWallVisible(false);
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


    //fire behabiour

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


    //sinking platform behaviour - sinks down when player is near, then resets after a delay

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
        while (transform.position.y > originPosition.y - 2f)
        {
            transform.position += Vector3.down * sinkSpeed * Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(sinkResetDelay);

        while (Vector3.Distance(transform.position, originPosition) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, originPosition, sinkSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = originPosition;
        isSinking          = false;
    }


    // pushing wall
    

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


    // killbox, fire, and wall collision logic
    //  obstacle kill logic + wall activation lives here

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        switch (obstacleType)
        {
            case ObstacleType.Killbox:
                KillPlayer(player, other.transform.position);
                break;

            case ObstacleType.Fire:
                KillPlayer(player, other.transform.position);
                break;

            case ObstacleType.TeleportingWall:
               
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
        Collider2D[]     hits    = Physics2D.OverlapCircleAll(transform.position, radius);
        PlayerController nearest = null;
        float            closest = Mathf.Infinity;

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
        Collider2D     col = GetComponent<Collider2D>();

        if (sr)  sr.enabled  = visible;
        if (col) col.enabled = visible;
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
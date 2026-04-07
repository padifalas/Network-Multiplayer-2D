using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class Obstacle : NetworkBehaviour
{
    public enum ObstacleType { Fire, Sink, TeleportingWall, Killbox }

    [Header("Type")]
    [SerializeField] private ObstacleType obstacleType;

    [Header("Fire Settings")]
    [SerializeField] private float fireExpandScale  = 2.5f;
    [SerializeField] private float fireExpandSpeed = 3f;
    [SerializeField] private float fireDetectRadius= 3f;

    [Header("Sink Settings")]
    [SerializeField] private float sinkSpeed  = 2f;
    [SerializeField] private float sinkDetectRadius = 2f;
    [SerializeField] private float sinkResetDelay  = 2f;

    [Header("Teleporting Wall Settings")]
    [SerializeField] private float wallMoveSpeed  = 8f;
    [SerializeField] private float wallTriggerRadius  = 6f;

    [Header("Particles")]
    [SerializeField] private ParticleSystem obstacleParticles;


    private Vector3 originPosition;
    private Vector3 originScale;
    private bool isActive;

    // fire
    private bool isExpanded;

    // sink
    private bool isSinking;
    private Coroutine sinkResetCoroutine;

    // wall
    private bool isCharging;
    private Transform  wallTarget;


    private void Start()
    {
        originPosition = transform.position;
        originScale = transform.localScale;
    }

    private void Update()
    {
        if (!IsServer) return;

        switch (obstacleType)
        {
            case ObstacleType.Fire:          
            HandleFire();          
            break;

            case ObstacleType.Sink:          
            HandleSink();          
            break;

            case ObstacleType.TeleportingWall: 
            HandleWall();        
            break;
        }
    }


//   fire
    // expands toward  player within  radius

    private void HandleFire()
    {
        PlayerController nearest = GetNearestPlayer(fireDetectRadius);

        if (nearest != null)
        {
            float target = fireExpandScale;
            transform.localScale = Vector3.Lerp(
                transform.localScale,
                originScale * target,
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


    // sink platform things
    // Sinks downward when a player is close, resets after delay

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
        isSinking = false;
    }


    // push wall
    // shows up n rushs  to the nearest player, pushing them

    private void HandleWall()
    {
        if (isCharging && wallTarget != null)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                wallTarget.position,
                wallMoveSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, wallTarget.position) < 0.5f)
            {
                isCharging = false;
                wallTarget = null;
                StartCoroutine(ResetWallRoutine());
            }
            return;
        }

        if (!isCharging)
        {
            PlayerController nearest = GetNearestPlayer(wallTriggerRadius);
            if (nearest != null)
            {
                wallTarget = nearest.transform;
                isCharging = true;
            }
        }
    }

    private IEnumerator ResetWallRoutine()
    {
        yield return new WaitForSeconds(1.5f);

        while (Vector3.Distance(transform.position, originPosition) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, originPosition, wallMoveSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = originPosition;
    }


    //killbox
    // anything that kills on contact... like fire, wall, killbox all route hereeeee

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        bool shouldKill = obstacleType == ObstacleType.Killbox
                       || obstacleType == ObstacleType.Fire
                       || obstacleType == ObstacleType.TeleportingWall;

        if (shouldKill)
        {
            player.Die();
            PlayDeathParticlesClientRpc(other.transform.position);
        }
    }


    

    private PlayerController GetNearestPlayer(float radius)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position, radius);

        PlayerController nearest = null;
        float closest = Mathf.Infinity;

        foreach (Collider2D hit in hits)
        {
            PlayerController p = hit.GetComponent<PlayerController>();
            if (p == null) continue;

            float dist = Vector3.Distance(transform.position, p.transform.position);
            if (dist < closest)
            {
                closest = dist;
                nearest = p;
            }
        }

        return nearest;
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
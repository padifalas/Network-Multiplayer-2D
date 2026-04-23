using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class GameManager : NetworkBehaviour
{
    public static GameManager Singleton { get; private set; }

    [Header("Spawn Points")]
    [SerializeField] private Transform spawnPointP1;
    [SerializeField] private Transform spawnPointP2;

    [Header("Scoring")]
    public NetworkVariable<int> Player1Score = new( 0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> Player2Score = new( 0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> RoundActive = new( true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);


    private void Awake()
    {
        if (Singleton != null) { Destroy(gameObject); return; }
        Singleton = this;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        MovePlayerToSpawn(0);
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        MovePlayerToSpawn(clientId);
    }

    private void MovePlayerToSpawn(ulong clientId)
    {
        StartCoroutine(MoveAfterSpawn(clientId));
    }

    private IEnumerator MoveAfterSpawn(ulong clientId)
    {
        yield return new WaitForSeconds(0.1f);

        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId)) yield break;

        NetworkObject playerObj = NetworkManager.Singleton
            .ConnectedClients[clientId].PlayerObject;

        if (playerObj == null) yield break;

        PlayerController controller = playerObj.GetComponent<PlayerController>();
        if (controller == null) yield break;

        Vector3 spawnPos = clientId == 0
            ? spawnPointP1.position
            : spawnPointP2.position;

        controller.SetSpawnPoint(spawnPos);
    }

    
    public void PlayerReachedDoor(ulong clientId)
    {
        if (!IsServer)  return;
        if (!RoundActive.Value) return;

        RoundActive.Value = false;

        if (clientId == 0) Player1Score.Value++;
        else Player2Score.Value++;
    }
}
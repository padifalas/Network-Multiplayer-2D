using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    [Header("Spawn Points")]
    [SerializeField] private Transform spawnPointP1;
    [SerializeField] private Transform spawnPointP2;

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

    private System.Collections.IEnumerator MoveAfterSpawn(ulong clientId)
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
}
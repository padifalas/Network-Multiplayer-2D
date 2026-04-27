using Unity.Netcode;
using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class GoalDoor : NetworkBehaviour
{
    [Header("Win Panels")]
    [SerializeField] private GameObject player1WinPanel;
    [SerializeField] private GameObject player2WinPanel;

    [Header("Settings")]
    [SerializeField] private float nextRoundDelay = 3f;
    [SerializeField] private string mainMenuScene  = "StartScene";

    private bool doorTaken;


    private void Start()
    {
        if (player1WinPanel != null) player1WinPanel.SetActive(false);
        if (player2WinPanel != null) player2WinPanel.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;
        if (doorTaken)  return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        doorTaken = true;

        GameManager.Singleton?.PlayerReachedDoor(player.OwnerClientId);
        ShowWinClientRpc(player.OwnerClientId == 0 ? 1 : 2);
        AudioManager.Singleton?.PlayGoal();
    }

    [ClientRpc]
    private void ShowWinClientRpc(int winningPlayer)
    {
        if (winningPlayer == 1)
        {
            if (player1WinPanel != null) player1WinPanel.SetActive(true);
        }
        else
        {
            if (player2WinPanel != null) player2WinPanel.SetActive(true);
        }

        AudioManager.Singleton?.PlayRoundWin();
    }


    // ── button callbacks ───────────────────────────────────────
    // wire these to the Replay and Quit buttons on both panels

    public void OnReplayPressed()
    {
        if (IsServer)
        {
            // server reloads the scene for everyone
            string sceneName = gameObject.scene.name;
            NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
        else
        {
            // client asks server to reload
            RequestReplayServerRpc();
        }
    }

public void OnQuitPressed()
{
    NetworkManager.Singleton.Shutdown();
    SceneManager.LoadScene(mainMenuScene);
}

    [ServerRpc(RequireOwnership = false)]
    private void RequestReplayServerRpc()
    {
        string sceneName = gameObject.scene.name;
        NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}
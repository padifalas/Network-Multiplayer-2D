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

    StopGameClientRpc();
}

    [ClientRpc]
    private void StopGameClientRpc()
    {
    // freeze all players
    PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
    foreach (PlayerController p in players)
    {
        Rigidbody2D rb = p.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated      = false;
        }

        // disable input by disabling the component
        p.enabled = false;
    }

    // stop all obstacles
    Obstacle[] obstacles = FindObjectsByType<Obstacle>(FindObjectsSortMode.None);
    foreach (Obstacle o in obstacles)
        o.enabled = false;

    AudioManager.Singleton?.StopMusic();
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



    public void OnReplayPressed()
    {
        if (IsServer)
        {
            // server reloads the scene for everyone
            NetworkManager.Singleton.SceneManager.LoadScene(
                gameObject.scene.name, LoadSceneMode.Single);
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
        NetworkManager.Singleton.SceneManager.LoadScene(
            gameObject.scene.name, LoadSceneMode.Single);
    }
}
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class GoalDoor : NetworkBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private TMPro.TextMeshProUGUI winText;

    [Header("Settings")]
    [SerializeField] private float nextRoundDelay = 3f;

    private bool doorTaken;


    private void Start()
    {
        if (winPanel != null) winPanel.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer)  return;
        if (doorTaken)  return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        doorTaken = true;

        int    winningPlayer = player.OwnerClientId == 0 ? 1 : 2;
        string message       = $"Player {winningPlayer} reached the door!";

        // award point and show result via GameManager
        GameManager.Singleton?.PlayerReachedDoor(player.OwnerClientId);

        ShowWinClientRpc(message);
        AudioManager.Singleton?.PlayGoal();
    }

    [ClientRpc]
    private void ShowWinClientRpc(string message)
    {
        if (winPanel != null) winPanel.SetActive(true);
        if (winText  != null) winText.text = message;

        AudioManager.Singleton?.PlayRoundWin();

        StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(nextRoundDelay);
        if (winPanel != null) winPanel.SetActive(false);
    }
}
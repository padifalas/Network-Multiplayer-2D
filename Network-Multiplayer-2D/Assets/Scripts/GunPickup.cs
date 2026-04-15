using Unity.Netcode;
using UnityEngine;

public class GunPickup : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private GameObject visualObject;

    private bool collected;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;
        if (collected) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        collected = true;

        player.EquipGun();
        HidePickupClientRpc();
    }

    [ClientRpc]
    private void HidePickupClientRpc()
    {
        if (visualObject != null) visualObject.SetActive(false);
    }
}
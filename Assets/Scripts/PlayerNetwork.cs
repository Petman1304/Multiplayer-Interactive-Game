using UnityEngine;
using Unity.Netcode;

public class PlayerNetwork : NetworkBehaviour
{
    // Nama pemain
    public NetworkVariable<string> playerName =
        new NetworkVariable<string>(
            "Player",
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

    // Role:
    // 0 = Belum memilih
    // 1 = Driver
    // 2 = Shooter
    public NetworkVariable<int> playerRole =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

    // Status Ready:
    // false = Belum Ready
    // true = Ready
    public NetworkVariable<bool> isReady =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Mencari UIManager di scene
            UIManager uiManager = FindAnyObjectByType<UIManager>();

            if (uiManager != null)
            {
                uiManager.SetLocalPlayer(this);

                Debug.Log("Player lokal berhasil didaftarkan ke UIManager!");
            }
            else
            {
                Debug.LogWarning("UIManager tidak ditemukan di scene!");
            }
        }
    }

    // Dipanggil ketika player memilih role
    public void SetRole(int roleIndex)
    {
        if (IsOwner)
        {
            playerRole.Value = roleIndex;

            if (roleIndex == 1)
            {
                Debug.Log("Role dipilih: Driver");
            }
            else if (roleIndex == 2)
            {
                Debug.Log("Role dipilih: Shooter");
            }
            else
            {
                Debug.Log("Role belum dipilih");
            }
        }
    }

    // Dipanggil ketika tombol Ready ditekan
    public void ToggleReady()
    {
        if (IsOwner)
        {
            isReady.Value = !isReady.Value;

            Debug.Log("Status Ready: " + isReady.Value);
        }
    }
}
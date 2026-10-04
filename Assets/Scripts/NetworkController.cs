using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;

public class NetworkController : MonoBehaviour
{
    public UIManager uiManager;
    private UnityTransport transport;

    void Start()
    {

        transport = GetComponent<UnityTransport>();
    }

    // Dipanggil saat tombol Host ditekan
    public void StartHostGame()
    {
        // Cek pengaman agar tidak error jika uiManager atau usernameInput kosong
        if (uiManager != null && uiManager.usernameInput != null)
        {
            Debug.Log("Starting Host with username: " + uiManager.usernameInput.text);
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.StartHost();

            if (uiManager != null)
            {
                uiManager.OpenRoomPanel();
            }
        }
        else
        {
            Debug.LogError("NetworkManager.Singleton tidak ditemukan!");
        }
    }

    // Dipanggil saat tombol Connect (Client) ditekan
    public void StartClientGame()
    {
        string ipAddress = uiManager.ipInput.text;

        // Jika IP kosong, default ke localhost (127.0.0.1) untuk uji coba di 1 PC
        if (string.IsNullOrEmpty(ipAddress))
        {
            ipAddress = "127.0.0.1";
        }

        // Set alamat IP tujuan
        transport.ConnectionData.Address = ipAddress;

        // Memulai Client Netcode
        NetworkManager.Singleton.StartClient();

        // Pindah tampilan ke Room Panel
        uiManager.OpenRoomPanel();
    }
}
using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject clientMenuPanel;
    public GameObject roomPanel;

    [Header("Inputs")]
    public TMP_InputField usernameInput;
    public TMP_InputField ipInput;

    private PlayerNetwork localPlayerNetwork;

    public void SetLocalPlayer(PlayerNetwork player)
    {
        localPlayerNetwork = player;
        Debug.Log("Player lokal berhasil terhubung ke UIManager!");
    }

    // Saat game pertama kali dibuka, pastikan hanya Main Menu yang aktif
    void Start()
    {
        ShowMainMenu();
    }

    public void ShowMainMenu()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (clientMenuPanel != null) clientMenuPanel.SetActive(false);
        if (roomPanel != null) roomPanel.SetActive(false);
    }

    public void OpenClientMenu()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (clientMenuPanel != null) clientMenuPanel.SetActive(true);
        if (roomPanel != null) roomPanel.SetActive(false);
    }

    public void BackToMainMenu()
    {
        ShowMainMenu();
    }

    public void OpenRoomPanel()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (clientMenuPanel != null) clientMenuPanel.SetActive(false);
        if (roomPanel != null) roomPanel.SetActive(true); // Hanya RoomPanel yang nyala!
    }

    public void OnClickDriver()
    {
        Debug.Log("Tombol Driver diklik!");
        if (localPlayerNetwork != null)
        {
            localPlayerNetwork.SetRole(1);
        }
    }

    public void OnClickShooter()
    {
        Debug.Log("Tombol Shooter diklik!");
        if (localPlayerNetwork != null)
        {
            localPlayerNetwork.SetRole(2);
        }
    }

    public void OnClickReady()
    {
        Debug.Log("Tombol Ready diklik!");
        if (localPlayerNetwork != null)
        {
            localPlayerNetwork.ToggleReady();
        }
    }
}
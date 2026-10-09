using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    [SerializeField] private TMP_InputField roomInputfield;
    [SerializeField] private GameObject lobbyUI;
    [SerializeField] private GameObject roomUI;
    [SerializeField] private TMP_Text roomName;

    [SerializeField] private RoomPrefab roomPrefab;
    private List<RoomPrefab> roomPrefabs = new List<RoomPrefab>();
    [SerializeField] private Transform contentObject;

    private void Start()
    {
        PhotonNetwork.JoinLobby();
    }

    public void CreateRoom()
    {
        if (roomInputfield.text != "")
        {
            PhotonNetwork.CreateRoom(roomInputfield.text);
        }
    }

    public override void OnJoinedRoom()
    {
        lobbyUI.SetActive(false);
        roomUI.SetActive(true);
        roomName.text = "ROOM: " + PhotonNetwork.CurrentRoom.Name;
    }
    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        UpdateRoomList(roomList);
    }

    private void UpdateRoomList(List<RoomInfo> roomlist)
    {
        foreach (RoomPrefab room in roomPrefabs)
        {
            Destroy(room.gameObject);
        }
        roomPrefabs.Clear();

        foreach (RoomInfo room in roomlist)
        {
            RoomPrefab newRoom = Instantiate(roomPrefab, contentObject);
            newRoom.SetRoomName(room.Name);
            roomPrefabs.Add(newRoom);
        }
    }
}
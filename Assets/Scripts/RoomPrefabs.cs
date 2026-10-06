using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class RoomPrefab : MonoBehaviour
{
    [SerializeField] private TMP_Text roomName;

    public void SetRoomName(string nameToDisplay)
    {
        roomName.text = nameToDisplay;
    }
}
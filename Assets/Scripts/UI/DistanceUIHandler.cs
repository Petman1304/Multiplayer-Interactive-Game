using UnityEngine;
using TMPro;

public class DistanceUIHandler : MonoBehaviour
{
    [SerializeField]
    TextMeshProUGUI distanceTravelledText;

    //reference
    TruckHandler playerTruckHandler;

    private void Awake()
    {
        playerTruckHandler = GameObject.FindGameObjectWithTag("Player").GetComponent<TruckHandler>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        distanceTravelledText.text = playerTruckHandler.DistanceTravelledZ.ToString("000000");

    }
}

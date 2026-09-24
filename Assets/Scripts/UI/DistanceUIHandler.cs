using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class DistanceUIHandler : MonoBehaviour
{
    [SerializeField]
    TextMeshProUGUI distanceTravelledText;

    [SerializeField]
    TextMeshProUGUI gameOverText;

    [SerializeField]
    CanvasGroup gameOverCanvasGroup;

    //reference
    TruckHandler playerTruckHandler;

    private void Awake()
    {
        playerTruckHandler = GameObject.FindGameObjectWithTag("Player").GetComponent<TruckHandler>();
        playerTruckHandler.OnPlayerCrashed += PlayerTruckHandler_OnPlayerCrashed;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gameOverCanvasGroup.interactable = false;
        gameOverCanvasGroup.alpha = 0;
    }

    // Update is called once per frame
    void Update()
    {
        distanceTravelledText.text = playerTruckHandler.DistanceTravelledZ.ToString("000000");
    }

    IEnumerator StartGameOverAnimationCO()
    {
        //yield return new WaitForSecondsRealtime(1.0f);

        gameOverCanvasGroup.interactable = true;

        while(gameOverCanvasGroup.alpha < 1.0f)
        {
            gameOverCanvasGroup.alpha = Mathf.MoveTowards(gameOverCanvasGroup.alpha, 1.0f, Time.deltaTime * 2);

            yield return null;
        }
    }

    void PlayerTruckHandler_OnPlayerCrashed(TruckHandler obj)
    {
        gameOverText.text = $"Points {distanceTravelledText.text}";

        StartCoroutine(StartGameOverAnimationCO());
    }

    public void OnRestartClicked()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}

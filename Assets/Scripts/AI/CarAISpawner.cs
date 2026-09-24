using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarAISpawner : MonoBehaviour
{
    [SerializeField]
    GameObject[] carAIPrefabs;

    GameObject[] carAIPool = new GameObject[20];

    Transform truckTransform;

    WaitForSeconds wait = new WaitForSeconds(0.5f);

    float timeLastCarSpawned = 0;

    [SerializeField]
    LayerMask otherCarsLayerMask;

    Collider[] overlappedCheckCollider = new Collider[1];

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        truckTransform = GameObject.FindGameObjectWithTag("Player").transform;

        int prefabIndex = 0;

        for(int i = 0; i < carAIPool.Length; i++)
        {
            carAIPool[i] = Instantiate(carAIPrefabs[prefabIndex]);
            carAIPool[i].SetActive(false);

            prefabIndex++;

            if (prefabIndex > carAIPrefabs.Length - 1)
                prefabIndex = 0;
        }

        StartCoroutine(UpdateLessOftenCO());
    }

    IEnumerator UpdateLessOftenCO()
    {
        while (true)
        {
            CleanUpCarsBeyondView();
            SpawnNewCar();
            yield return wait; 
        }
    }

    void SpawnNewCar()
    {
        if (Time.time - timeLastCarSpawned < 2)
            return;

        GameObject carToSpawn = null;

        foreach (GameObject aiCar in carAIPool)
        {
            if (aiCar.activeInHierarchy)
                continue;

            carToSpawn = aiCar;
            break;
        }

        if (carToSpawn == null)
            return;

        float xSpawn;
        if (Random.Range(-1.0f, 1.0f) > 0)
            xSpawn = 10.0f;
        else
            xSpawn = -10.0f;

        Vector3 spawnPosition = new Vector3(xSpawn, 0, truckTransform.position.z + 400);


        if (Physics.OverlapBoxNonAlloc(spawnPosition, Vector3.one * 2, overlappedCheckCollider, Quaternion.identity, otherCarsLayerMask) > 0)
            return;


        carToSpawn.transform.position = spawnPosition;
        carToSpawn.SetActive(true);

        timeLastCarSpawned = Time.time;
        Debug.Log($"timeLastCarSpawned : {timeLastCarSpawned}");
        Debug.Log($"Spawn position : {spawnPosition}");

    }

    void CleanUpCarsBeyondView()
    {
        foreach(GameObject aiCar in carAIPool)
        {
            if (!aiCar.activeInHierarchy)
                continue;

            if (aiCar.transform.position.z - truckTransform.position.z > 500)
                aiCar.SetActive(false);

            if (aiCar.transform.position.z - truckTransform.position.z < -50)
                aiCar.SetActive(false);
        }
    }
}

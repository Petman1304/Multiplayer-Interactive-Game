using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndlessLevelHandler : MonoBehaviour
{
    [SerializeField]
    GameObject[] sectionPrefabs;

    GameObject[] sectionPool = new GameObject[20];

    GameObject[] sections = new GameObject[10];

    Transform truckTransform;

    WaitForSeconds waitFor100Ms = new WaitForSeconds(0.1f);

    const float sectionLength = 500;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        truckTransform = GameObject.FindGameObjectWithTag("Player").transform;

        int prefabsIndex = 0;

        for(int i = 0; i < sectionPool.Length; i++)
        {
            sectionPool[i] = Instantiate(sectionPrefabs[prefabsIndex]);
            sectionPool[i].SetActive(false);

            prefabsIndex++;
            if (prefabsIndex > sectionPrefabs.Length - 1)
                prefabsIndex = 0;
        }

        for(int i = 0; i < sections.Length; i++)
        {
            GameObject randomSection = GetRandomSectionFromPool();

            randomSection.transform.position = new Vector3(sectionPool[i].transform.position.x, 0, i * sectionLength);
            randomSection.SetActive(true);

            sections[i] = randomSection;
        }

        StartCoroutine(UpdateLessOftenCO());
    }

    IEnumerator UpdateLessOftenCO()
    {
        while (true)
        {
            UpdateSectionPositions();
            yield return waitFor100Ms;
        }
    }

    void UpdateSectionPositions()
    {
        for (int i = 0; i < sections.Length; i++)
        {
            if(sections[i].transform.position.z - truckTransform.position.z < -sectionLength)
            {
                Vector3 lastSectionPosition = sections[i].transform.position;
                sections[i].SetActive(false);

                sections[i] = GetRandomSectionFromPool();

                sections[i].transform.position = new Vector3(lastSectionPosition.x, 0, lastSectionPosition.z + sectionLength * sections.Length);
                sections[i].SetActive(true);
            }
        }
    }

    GameObject GetRandomSectionFromPool()
    {
        int randomIndex = Random.Range(0, sectionPool.Length);

        bool isSectionFound = false;

        while (!isSectionFound)
        {
            if (!sectionPool[randomIndex].activeInHierarchy)
            {
                isSectionFound = true;
            }
            else
            {
                randomIndex++;
                if (randomIndex > sectionPool.Length - 1)
                    randomIndex = 0;
            }
        }

        return sectionPool[randomIndex];
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// TODO : Fix car ai handler script

public class CarAIHandler : MonoBehaviour
{
    [SerializeField]
    TruckHandler carHandler;

    [SerializeField]
    LayerMask otherCarsLayerMask;

    [SerializeField]
    MeshCollider meshCollider;

    //Collision detection
    RaycastHit[] raycastHits = new RaycastHit[1];
    bool isCarAhead = false;

    //Timing
    WaitForSeconds wait = new WaitForSeconds(0.2f);

    private void Awake()
    {
        if (CompareTag("Player"))
        {
            Destroy(this);
            return;
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(UpdateLessOftenCO());
    }

    // Update is called once per frame
    void Update()
    {
        float accelerationInput = 0.0f;
        float steerInput = 0.0f;

        if (isCarAhead)
            accelerationInput = -1.0f;

        steerInput = Mathf.Clamp(steerInput, -1.0f, 1.0f);

        carHandler.SetInput(new Vector2(steerInput, accelerationInput));

        Debug.Log("AI Updated");
    }

    IEnumerator UpdateLessOftenCO()
    {
        while (true)
        {
            isCarAhead = CheckCarAhead();
            yield return wait;
        }
    }

    bool CheckCarAhead()
    {
        meshCollider.enabled = false;

        int numberOfHits = Physics.BoxCastNonAlloc(transform.position, Vector3.one * 0.25f, transform.forward, raycastHits, Quaternion.identity, 1, otherCarsLayerMask);

        meshCollider.enabled = true;

        if (numberOfHits > 0)
            return true;
        
        return false;
    }

    //Events
    private void OnEnable()
    {
        carHandler.SetMaxVelocity(Random.Range(100, 200));
    }
}

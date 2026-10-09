using System.Collections;
using UnityEngine;

public class GunController : MonoBehaviour
{
    [SerializeField]
    SerialGyroInputHandler gyroInputHandler;

    [SerializeField]
    float aimSensitivity = 1.0f;

    [SerializeField]
    private GameObject bulletPrefab;

    private Vector3 _gyroInput = Vector3.zero;
    private Vector3 _currentRot;
    private Transform cameraTransform;

    [SerializeField]
    private Transform barrelTransform;

    private WaitForSeconds fireRate = new WaitForSeconds(0.5f);
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _currentRot = Vector3.zero;
        cameraTransform = Camera.main.transform;

        StartCoroutine(FireRateUpdate());

    }

    // Update is called once per frame
    void Update()
    {
        //if (gyroInputHandler.resetAngle == 1)
        //{
        //    ShootGun();
        //}

        _gyroInput = gyroInputHandler.rotAngle;
        _currentRot = new Vector3(-_gyroInput.y, _gyroInput.z, 0);

        transform.rotation = Quaternion.Euler(_currentRot * aimSensitivity);
    }

    void ShootGun()
    {
        if (gyroInputHandler.resetAngle == 0)
            return;

        RaycastHit hit;
        GameObject bullet = GameObject.Instantiate(bulletPrefab, barrelTransform.position, Quaternion.identity);
        BulletController bulletController = bullet.GetComponent<BulletController>();
        if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out hit, Mathf.Infinity))
        {
            
            bulletController.target = hit.point;
            bulletController.hit = true;
        }
        else
        {
            bulletController.target = cameraTransform.position + cameraTransform.forward * 25f;
            bulletController.hit = true;
        }
    }

    IEnumerator FireRateUpdate()
    {
        while (true)
        {
            ShootGun();
            yield return fireRate;
        }
    }
}

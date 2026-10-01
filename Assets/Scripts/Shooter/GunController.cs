using UnityEngine;

public class GunController : MonoBehaviour
{
    [SerializeField]
    SerialGyroInputHandler gyroInputHandler;

    [SerializeField]
    float aimSensitivity = 1.0f;

    private Vector3 _gyroInput = Vector3.zero;
    private Vector3 _currentRot;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _currentRot = Vector3.zero;
    }

    // Update is called once per frame
    void Update()
    {
        if (gyroInputHandler.resetAngle == 1)
        {
            _currentRot = Vector3.zero;
        }

        _gyroInput = gyroInputHandler.rotSpeed * Time.deltaTime;
        _currentRot -= new Vector3(_gyroInput.x, _gyroInput.z, 0);

        transform.rotation = Quaternion.Euler(_currentRot * aimSensitivity);
    }
}

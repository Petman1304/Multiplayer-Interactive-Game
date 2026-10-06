using System.Globalization;
using UnityEngine;
using System.IO.Ports;

public class SerialGyroInputHandler : MonoBehaviour
{
    [SerializeField]
    string portName;

    public Vector3 rotSpeed;
    public Vector3 rotAngle;
    public int resetAngle;

    private SerialPort serial;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        serial = new SerialPort(portName, 115200);
        serial.ReadTimeout = 10;

        rotAngle = Vector3.zero;

        try
        {
            serial.Open();
        }
        catch (System.Exception e) 
        {
            Debug.LogError($"Failed to open serial port {portName} : {e.Message}");
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (serial == null || !serial.IsOpen)
            return;

        //if (resetAngle == 1)
        //    rotAngle = Vector3.zero;

        try
        {
            string data = serial.ReadLine().Trim();

            string[] values = data.Split(',');

            if (values.Length != 4)
                return;

            if (
                float.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float rotX) &&
                float.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float rotY) &&
                float.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float rotZ) &&
                int.TryParse(values[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int reset)
        ) {
                rotSpeed = new Vector3(rotX, rotY, rotZ);
                rotAngle -= rotSpeed * Time.deltaTime;
                resetAngle = reset;
            } 
        }
        catch (System.TimeoutException)
        {

        }
    }

    private void OnDestroy()
    {
        if (serial != null && serial.IsOpen)
            serial.Close();
    }
}

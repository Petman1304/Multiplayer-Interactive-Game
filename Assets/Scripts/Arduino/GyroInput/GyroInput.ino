// Basic demo for accelerometer readings from Adafruit MPU6050

// ESP32 Guide: https://RandomNerdTutorials.com/esp32-mpu-6050-accelerometer-gyroscope-arduino/
// ESP8266 Guide: https://RandomNerdTutorials.com/esp8266-nodemcu-mpu-6050-accelerometer-gyroscope-arduino/
// Arduino Guide: https://RandomNerdTutorials.com/arduino-mpu-6050-accelerometer-gyroscope/

#include <Adafruit_MPU6050.h>
#include <Adafruit_Sensor.h>
#include <Wire.h>

float deadzone(float data);


// data

sensors_event_t a, g, temp;

float rotX, rotY, rotZ;

float Ax, Ay, Az;
float Gx, Gy, Gz;

float roll, pitch, yaw;

float epsilon = 0.1;
int resetBtn = 6;

//calibration
float xMax = 1.02;
float xMin = -0.96;
float yMin = -1.03;
float yMax = 0.98;
float zMax = 1;
float zMin = -1.05;


float xOffset = (xMax + xMin)/2;
float yOffset = (yMax + yMin)/2;
float zOffset = (zMax + zMin)/2;

float xScale = 2 / (xMax - xMin);
float yScale = 2 / (yMax - yMin);
float zScale = 2 / (zMax - zMin);

float yawOffset;


Adafruit_MPU6050 mpu;

unsigned long lastMillis, currentMillis;
float dt;

void setup(void) {
  Serial.begin(115200);
  while (!Serial)
    delay(10); // will pause Zero, Leonardo, etc until serial console opens

  Serial.println("Adafruit MPU6050 test!");

  // Try to initialize!
  if (!mpu.begin()) {
    Serial.println("Failed to find MPU6050 chip");
    while (1) {
      delay(10);
    }
  }
  Serial.println("MPU6050 Found!");

  mpu.setAccelerometerRange(MPU6050_RANGE_8_G);
  Serial.print("Accelerometer range set to: ");
  switch (mpu.getAccelerometerRange()) {
  case MPU6050_RANGE_2_G:
    Serial.println("+-2G");
    break;
  case MPU6050_RANGE_4_G:
    Serial.println("+-4G");
    break;
  case MPU6050_RANGE_8_G:
    Serial.println("+-8G");
    break;
  case MPU6050_RANGE_16_G:
    Serial.println("+-16G");
    break;
  }
  mpu.setGyroRange(MPU6050_RANGE_1000_DEG);
  Serial.print("Gyro range set to: ");
  switch (mpu.getGyroRange()) {
  case MPU6050_RANGE_250_DEG:
    Serial.println("+- 250 deg/s");
    break;
  case MPU6050_RANGE_500_DEG:
    Serial.println("+- 500 deg/s");
    break;
  case MPU6050_RANGE_1000_DEG:
    Serial.println("+- 1000 deg/s");
    break;
  case MPU6050_RANGE_2000_DEG:
    Serial.println("+- 2000 deg/s");
    break;
  }

  mpu.setFilterBandwidth(MPU6050_BAND_5_HZ);
  Serial.print("Filter bandwidth set to: ");
  switch (mpu.getFilterBandwidth()) {
  case MPU6050_BAND_260_HZ:
    Serial.println("260 Hz");
    break;
  case MPU6050_BAND_184_HZ:
    Serial.println("184 Hz");
    break;
  case MPU6050_BAND_94_HZ:
    Serial.println("94 Hz");
    break;
  case MPU6050_BAND_44_HZ:
    Serial.println("44 Hz");
    break;
  case MPU6050_BAND_21_HZ:
    Serial.println("21 Hz");
    break;
  case MPU6050_BAND_10_HZ:
    Serial.println("10 Hz");
    break;
  case MPU6050_BAND_5_HZ:
    Serial.println("5 Hz");
    break;
  }

  float yawSum = 0;
  for(int i = 0; i < 100; i++){
    mpu.getEvent(&a, &g, &temp);
    yawSum += g.gyro.z;
    delay(50);
  }

  yawOffset = yawSum / 100;
  Serial.print("yawOffset:");
  Serial.println(yawOffset);


  Serial.println("");

  pinMode(5, INPUT);

  lastMillis = millis();
}

void loop() {
  /* Get new sensor events with the readings */
  mpu.getEvent(&a, &g, &temp);
  
  Ax = a.acceleration.x / 9.81;
  Ax = (Ax - xOffset) * xScale;
  Ay = a.acceleration.y / 9.81;
  Ay = (Ay - yOffset) * yScale;
  Az = a.acceleration.z / 9.81;
  Az = (Az - zOffset) * zScale;

  pitch = atan2(Ay, sqrt(Az*Az + Ax*Ax));
  roll = atan2(Ax, sqrt(Az*Az + Ay*Ay));

  currentMillis = millis();
  dt = (currentMillis - lastMillis);
  if(abs(g.gyro.z - yawOffset) > 0.01){
    yaw -= (g.gyro.z - yawOffset)*dt*0.001 ;
  }
  lastMillis = currentMillis;
  

  // Serial.print("Ax:");
  // Serial.print(Ax);
  // Serial.print(",");
  // Serial.print("Ay:");
  // Serial.print(Ay);
  // Serial.print(",");
  // Serial.print("Az:");
  // Serial.print(Az);
  // Serial.print(",");
  // Serial.print("UL:");
  // Serial.print(1);
  // Serial.print(",");
  // Serial.print("IL:");
  // Serial.println(-1);

  // Serial.print("roll:");
  Serial.print(roll);
  Serial.print(",");
  // Serial.print("pitch:");
  Serial.print(pitch);
  Serial.print(",");
  // Serial.print("yaw:");
  Serial.print(yaw);
  Serial.print(",");
  Serial.println(digitalRead(5));

  delay(50);
}


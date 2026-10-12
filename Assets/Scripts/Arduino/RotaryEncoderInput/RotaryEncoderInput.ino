int pinA = 2;
int pinB = 3;
int pinSwitch = 4;
int encoderPinAPrev, encoderPinACurr, encoderPinB;
int pulseCount;
float rotAngle;
float toRad = 2*3.14 / 40;
bool bool_CW;


void setup() {
  Serial.begin(9600);
  pinMode(pinA, INPUT);
  pinMode(pinB, INPUT);
  pinMode(pinSwitch, INPUT_PULLUP);

  pulseCount = 0;

  encoderPinAPrev = digitalRead(pinA); 
}

void loop() {
  encoderPinACurr = digitalRead(pinA);
  //check if rotate
  if(encoderPinACurr != encoderPinAPrev){
    
    encoderPinB = digitalRead(pinB);
    // CCW
    if(encoderPinB == encoderPinACurr){
      bool_CW = false;
      pulseCount++;
    }
    // CW
    else{
      bool_CW = true;
      pulseCount--;
    }
  }

  encoderPinAPrev = encoderPinACurr;
  rotAngle = pulseCount * toRad;
  
  Serial.println(rotAngle);
  delay(1);
}



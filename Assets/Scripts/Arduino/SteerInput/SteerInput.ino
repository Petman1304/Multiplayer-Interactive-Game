int potValue;
float out;


void setup() {
  Serial.begin(9600);  
}

void loop() {
  potValue = analogRead(A0);
  out = (float) potValue / 2048 - 1.0;
  Serial.println(out);
  delay(1);
}

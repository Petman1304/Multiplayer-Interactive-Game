int potValue;
float out, filteredPotValue;


void setup() {
  Serial.begin(9600);  
}

void loop() {
  filteredPotValue = 0;
  for(int i = 0; i < 10; i++){
    potValue = analogRead(A0);
    filteredPotValue += (float) potValue/10;
    delay(1);
  }
  out = filteredPotValue / 2048 - 1.0;
  Serial.println(out);
  delay(100);
}

#include <Arduino.h>
#define BAUD 2000000
#define SAMPLE_DELAY_MS 100

int fsr1 = 0;
int fsr2 = 0;
int fsr3 = 0;
int fsr4 = 0;

void setup()

{
Serial.begin(BAUD);
analogReadResolution(10);

while (!Serial)
{
 ;
}
Serial.println("A0,A1,A2,A3");
}

void loop()

{
fsr1 = analogRead(A0);
fsr2 = analogRead(A1);
fsr3 = analogRead(A2);
fsr4 = analogRead(A3);

Serial.print(fsr1);
Serial.print(",");
Serial.print(fsr2);
Serial.print(",");
Serial.print(fsr3);
Serial.print(",");
Serial.println(fsr4);

delay(SAMPLE_DELAY_MS);

}
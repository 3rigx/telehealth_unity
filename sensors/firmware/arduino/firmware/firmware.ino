#define BAUD 115200
#define R_KNOWN 220
#define ANALOGUE_RES 103
#define VCC 5
#define MOCK
uint8_t fsr1 = 0, fsr2, fsr3, fsr4;

uint8_t readFSR(int pin)
{
	return (1 - (analogRead(pin) / ANALOGUE_RES)) * 256;
}

uint8_t rnd(uint8_t prev = 0){
  return (uint8_t) max(0,min(255,((long)prev) + random(-30,30) ));
}


float measurev(int pin){
  return analogRead(pin) * VCC/ANALOGUE_RES;
}

float measurer(int pin){
  return R_KNOWN * ( (analogRead(pin)/ANALOGUE_RES) - 1);
}

void setup()
{
	Serial.begin(BAUD);
}

void loop()
{
	if (Serial.available())
	{
		uint8_t incoming = Serial.read();
		if (incoming == 'r') // Read command sent
		{
#ifdef MOCK
      fsr1 = rnd(fsr1);
      fsr2 = rnd(fsr2);
      fsr3 = rnd(fsr3);
      fsr4 = rnd(fsr4);
#else
      fsr1 = readFSR(0);
      fsr2 = readFSR(1);
      fsr3 = readFSR(2);
      fsr4 = readFSR(3);
#endif
			Serial.print(fsr1, DEC);
			Serial.write(',');
			Serial.print(fsr2,DEC);
			Serial.write(',');
			Serial.print(fsr3,DEC);
			Serial.write(',');
			Serial.print(fsr4,DEC);
			Serial.print('\n');
		}
	}
}

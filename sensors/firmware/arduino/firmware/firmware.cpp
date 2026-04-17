#ifdef MOCK
#include <time.h>
#include <stdlib.h>
#endif
#include <stdint.h>
#include <Arduino.h>

#define BAUD 115200
#define R_KNOWN 220
#define ANALOGUE_RES 1023
#define VCC 5

#ifndef max
#define max(a, b) ((a) < (b) ? (b) : (a))
#endif
#ifndef min
#define min(a, b) ((a) < (b) ? (a) : (b))
#endif

extern HardwareSerial Serial;

uint8_t fsr1 = 0, fsr2, fsr3, fsr4;

uint8_t readFSR(int pin)
{
  return (1 - (analogRead(pin) / ANALOGUE_RES)) * 256;
}

#ifdef MOCK
srand(time(NULL));
uint8_t rnd(uint8_t prev)
{
  long rand = prev + (rand() % 61) - 30;
  return (uint8_t)max(0, min(255, (rand)));
}
#endif

float measurev(int pin)
{
  return analogRead(pin) * VCC / ANALOGUE_RES;
}

float measurer(int pin)
{
  return R_KNOWN * ((analogRead(pin) / ANALOGUE_RES) - 1);
}

void setup()
{
  Serial.begin(BAUD);
}

void loop()
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
  Serial.print(analogRead(0), DEC);
  Serial.write(':');
  Serial.print(fsr1, DEC);
  Serial.write(',');
  Serial.print(fsr2, DEC);
  Serial.write(',');
  Serial.print(fsr3, DEC);
  Serial.write(',');
  Serial.print(fsr4, DEC);
  Serial.print('\n');
  delay(1000);
}

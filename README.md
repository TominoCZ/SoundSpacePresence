# Sound Space Presence
This program tracks the bottom-right pixel of a game window and sends its color to a remote device over Bluetooth to display it.

This was primarily made to work with my game [Sound Space](https://www.roblox.com/games/2677609345/Sound-Space), however you can implement this into your games too, read further to learn how, you'll have to do some programming there since this is only the Client that sends a color to a remote device.

[Here's a video of it in action](https://www.youtube.com/watch?v=rbYCVzi9IPo&t=45s&ab_channel=Morphox)

## Connecting to the device
The paired device's MAC address is passed in as a launch parameter.

## The remote device
I recommend using an ESP32 or an Ardunio with a Bluetooth module for this. The device will control the lighting effects like a color wave on and LED strip.

The data sent to the device is in this format: ``{command}|{data}``, for example the data for a color wave command looks like this: ``w|255,0,255``.

**IMPORTANT:** Each message sent by this Client is separated by the ``$`` character by default so the raw data (when buffered) will look like this: ``w|255,0,255$w|0,255,0$``, you can change the data separator if you like.

The remote device needs to constantly send a ping message (can be empty AKA ``$``), otherwise the Client disconnects after the Timeout period since the last received message has passed.

You can send commands just by typing them into the console window.

# ESP32/Arduino example code
```C++
#include <BluetoothSerial.h>

BluetoothSerial SerialBT;

// The buffered received Bluetooth data
String buffer = "";
// The last received command type
String mode = "";
// The message separator
char stopChar = '$';
// Last time a ping was sent
long lastPing = 0;

void setup()
{
  Serial.begin(9600);

  Serial.println("Initializing Bluetooth...");
  SerialBT.begin("BTLights");
  while (!SerialBT.isReady())
  {
    delay(500);
  }
  Serial.println("Bluetooth is ready!");
}

// Write a message back to the Client
void write(String msg)
{
  // Escaping data separators
  msg.replace(String(stopChar), String('\\') + stopChar);
  
  SerialBT.print(msg + stopChar);
}

// Work with the received Wave color
void wave(byte r, byte g, byte b)
{
  //TODO: Do whatever you want with the color
}

// Separates a string like "255,255,255" into RGB values
bool parseColor(String data, byte *r, byte *g, byte *b)
{
  uint32_t len = data.length();
  String last = "";

  int count = 0;

  for (uint32_t i = 0; i < len; i++)
  {
    char c = data[i];

    if (c == ',' || i == len - 1)
    {
      if (i == len - 1)
        last += c;
      if (count == 0)
        *r = byte(last.toInt());
      else if (count == 1)
        *g = byte(last.toInt());
      else if (count == 2)
        *b = byte(last.toInt());

      count++;
      last = "";
    }
    else
    {
      last += c;
    }
  }

  return count == 3;
}

// Process the Wave command
bool processWaveColor(String data)
{
  byte r = 0;
  byte g = 0;
  byte b = 0;

  if (parseColor(data, &r, &g, &b))
  {
    wave(r, g, b);

    return true;
  }

  return false;
}

// Process the received command
bool process(String msg, String &command)
{
  uint32_t len = msg.length();

  bool foundData = false;
  String cmd = "";
  String data = "";

  for (uint32_t i = 0; i < len; i++)
  {
    char c = msg[i];

    if (!foundData && c == '|')
    {
      foundData = true;
      continue;
    }

    if (foundData)
      data += c;
    else
      cmd += c;
  }

  command = cmd;

  //TODO: This is where you can add your custom commands
  if (cmd == "w")
  {
    return processWaveColor(data);
  }
  else if (cmd == "echo")
  {
    write(data);
  }

  return false;
}

// Read data sent by the Client over Bluetooth
void read()
{
  // Buffer available data sent by the Client
  while (SerialBT.available() > 0)
  {
    char c = SerialBT.read();

    if (c < 32)
      continue;

    buffer += c;
  }

  uint32_t len = buffer.length();
  String last = "";

  // Separate buffered data by the splitChar
  for (uint32_t i = 0; i < len; i++)
  {
    char c = buffer[i];
    // Handling data separation and escaped data separators
    if ((c == stopChar) && ((i == 0) || (buffer[i - 1] != '\\')))
    {
      String command = "";
      if (process(last, command))
      {
        mode = command;

        lastCommand = millis();
      }
      last = "";
    }
    else if ((c != '\\') || ((i < (len - 1)) && (buffer[i + 1] != stopChar)))
    {
      last += c;
    }
  }

  // Makes sure that we keep the data if there is a message only partially received
  buffer = last;
}

// Ping the Client device to let it know we're still connected
void ping()
{
  if (!SerialBT.hasClient())
    return;

  if (millis() - lastPing >= 750)
  {
    write("");

    lastPing = millis();
  }
}

void loop()
{
  ping();
  read();

  if (mode == "w")
  {
    //TODO: Maybe animate the wave
  }
}
```

# Sound Space Presence

It's primarily made to work with my game [Sound Space](https://www.roblox.com/games/2677609345/Sound-Space), however you can implement this into your games too.
To do that, you set the name of the process the program is looking for in the code, then you render a pixel with the color in the bottom-right corner of the window.

## Connecting to the device
The device's MAC address is passed as a launch parameter, the device must be paired.

## The remote device
The remote device needs to constantly send a ping message (can be empty), otherwise the Client program disconnects after the Timeout period since the last received message has passed.

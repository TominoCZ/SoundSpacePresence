# Sound Space Presence
It's primarily made to work with my game [Sound Space](https://www.roblox.com/games/2677609345/Sound-Space), however you can implement this into your games too.

## Connecting to the device
The paired device's MAC address is passed in as a launch parameter.

## The remote device
The remote device needs to constantly send a ping message (can be empty), otherwise the Client program disconnects after the Timeout period since the last received message has passed.

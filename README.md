# Sound Space Presence
This program tracks the bottom-right pixel of a game window and sends its color to a remote device over Bluetooth to display it.

This was primarily made to work with my game [Sound Space](https://www.roblox.com/games/2677609345/Sound-Space), however you can implement this into your games too.

[Here's a video of it in action](https://www.youtube.com/watch?v=rbYCVzi9IPo&t=45s&ab_channel=Morphox)

## Connecting to the device
The paired device's MAC address is passed in as a launch parameter.

## The remote device
The remote device needs to constantly send a ping message (can be empty), otherwise the Client program disconnects after the Timeout period since the last received message has passed.

The remote device animates the color sent to it.

The data sent to the device is in this format: ``{command}|{data}``, for example the data for a color wave command looks like this: ``w|255,0,255``.

**IMPORTANT:** Each data segment is separated by the ``$`` character by default so the raw data (when buffered) will look like this: ``w|255,0,255$w|0,255,0$``, you can change the data separator.

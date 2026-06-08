# WindowsGSM.Satisfactory
WindowsGSM plugin that provides Satisfactory Dedicated server support!

This plugin is more or less a straight copy from dkdue's [WindowsGSM.Valheim](https://github.com/dkdue/WindowsGSM.Valheim) (Thank you!) and still Work in Progress.
Feel free to create a Pull Request if you see room for improvement!
No guarantee for support, though...

# The Game
https://store.steampowered.com/app/526870/Satisfactory/

# Requirements
WindowsGSM >= 1.21.1

# Server Documentation
Official documentation about the dedicated server is available at https://satisfactory.fandom.com/wiki/Dedicated_servers

# Config

There are three relevant ports (all UDP) to open for incoming connections:

| Name |  Default | Description |
| -- |  -- | -- |
| ServerQueryPort | 15777 | can be changed freely |
| BeaconPort | 15000 | can't be changed. Increases automatically for every server instance |
| GamePort | 7777 | can't be changed. Increases automatically for every server instance |

# Known issues

Several :) i.e.:

- WindowsGSM does not recognize the server as running, it stays in the state "Starting" forever.
- WindowsGSM requires a variable for MaxPlayers, but the satisfactory server does not support this.

# Installation
  1. Download the latest release
  2. Move Satisfactory.cs folder to plugins folder
  3. Click [RELOAD PLUGINS] button or restart WindowsGSM

# License
This project is licensed under the MIT License
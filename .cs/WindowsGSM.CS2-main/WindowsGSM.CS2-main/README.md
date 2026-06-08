# WindowsGSM.CS2
🧩 WindowsGSM plugin that provides Counter Strike 2 Dedicated server support!


# WindowsGSM Installation: 
1. Download  WindowsGSM https://windowsgsm.com/ 
2. Create a Folder at a Location you wan't all Server to be Installed and Run.
4. Drag WindowsGSM.Exe into previoulsy created folder and execute it.

# Plugin Installation:
1. Download [latest](https://github.com/ohmcodes/WindowsGSM.CS2/releases/latest) release
2. Extract then Move **CS2.cs** folder to **plugins** folder
3. OR Press on the Puzzle Icon in the left bottom side and install this plugin by navigating to it and select the Zip File.
4. Click **[RELOAD PLUGINS]** button or restart WindowsGSM
5. Navigate "Servers" and Click "Install Game Server" and find "Counter Strike 2 Dedicated Server [CS2.cs]

### Official Documentation
🗃️ https://developer.valvesoftware.com/wiki/Counter-Strike_2/Dedicated_Servers

### The Game
🕹️ https://store.steampowered.com/app/730/CounterStrike_2/

### Dedicated server info
🖥️ https://steamdb.info/app/730/info/

# Automatic parameters via WindowsGSM.cfg
### To navigate this click the Edit config button on selected server
```
hostname => Server Name
map => Server Start Map
port => Server Port
maxplayers => Server Maxplayer
```

# Available parameters
```
-insecure	Allows VAC to be disabled.
+sv_logfile 1	Log server information in the log file.
+sv_lan 0	Server is a lan server ( no heartbeat, no authentication, no non-class C addresses ).
+game_alias deathmatch	Set the configuration of game type and mode based on game alias like 'deathmatch'.
+game_type 2 See Settings below.
+game_mode 0 See Settings below.
```

# Set Gamemodes
### add param +game_alias competitive
```
Competitive:
game_alias competitive <- sets both game mode and game type commands
game_mode 1
game_type 0

Wingman
game_alias wingman <- sets both game mode and game type commands
game_mode 2
game_type 0

Casual
game_alias casual <- sets both game mode and game type commands
game_mode 0
game_type 0

Deathmatch
game_alias deathmatch <- sets both game mode and game type commands
game_mode 2
game_type 1

Custom
game_alias custom <- sets both game mode and game type commands
game_mode 0
game_type 3
```

# Registering Game Server Login Token (GSLT)
#### To navigate this on WGSM click Edit config button and you will see Server GSLT field

1. Generate a token for your game server [View](http://steamcommunity.com/dev/managegameservers) 
2. App ID of the base game (e.g. 440 for TF2, 730 for CS:GO)
3. Memo (text stored with the account, just shown here to help you keep track)
4. Paste the Login Token on your WGSM GSLT text field.


# Set up cfg
Directory ``` \game\csgo\cfg\server.cfg ```
automation WIP

# NOTE
- Console Embed is not supported
- Server Stop/Restart is not supported you have to manually type quit in the console (need to test but fixed)
- Install Steam Launcher if the server is not starting

# License
This project is licensed under the MIT License - see the <a href="https://github.com/ohmcodes/WindowsGSM.CS2/blob/main/LICENSE">LICENSE.md</a> file for details

# WindowsGSM.AmericanTruckSimulator
🧩 WindowsGSM plugin that provides American Truck Simulator Dedicated server support!


# WindowsGSM Installation: 
1. Download  WindowsGSM https://windowsgsm.com/ 
2. Create a Folder at a Location you wan't all Server to be Installed and Run.
4. Drag WindowsGSM.Exe into previoulsy created folder and execute it.

# Plugin Installation:
1. Download [latest](https://github.com/ohmcodes/WindowsGSM.AmericanTruckSimulator/releases/latest) release
2. Extract then Move **AmericanTruckSimulator.cs** folder to **plugins** folder
3. OR Press on the Puzzle Icon in the left bottom side and install this plugin by navigating to it and select the Zip File.
4. Click **[RELOAD PLUGINS]** button or restart WindowsGSM
5. Navigate "Servers" and Click "Install Game Server" and find "American Truck Simulator Dedicated Server [AmericanTruckSimulator.cs]

### Official Documentation
🗃️ https://steamcommunity.com/sharedfiles/filedetails/?id=2900693664

### Unofficial Documentation
🗃️ https://www.ionos.ca/digitalguide/server/know-how/american-truck-simulator-server/

### The Game
🕹️ https://store.steampowered.com/app/270880/American_Truck_Simulator/

### Dedicated server info
🖥️ https://steamdb.info/app/2239530/info/


# Registering Game Server Login Token (GSLT)
#### To navigate this on WGSM click Edit config button and you will see Server GSLT field

1. Generate a token for your game server [View](http://steamcommunity.com/dev/managegameservers) 
2. App ID of the base game (e.g. 440 for TF2, 730 for CS:GO)
3. Memo (text stored with the account, just shown here to help you keep track)
4. Paste the Login Token on your WGSM GSLT text field.

# Automatic parameters via WindowsGSM.cfg to server_cfg.ini
### To navigate this click the Edit config button on selected server
```
lobby_name => Server Name
connection_dedicated_port => Server Port
query_dedicated_port => Server Query Port
max_players => Server Maxplayer (DONT CHANGE)
```

# NOTE
- before intalling the server it is better to do step 1 - 8
- max_players this can't be more than 8 
- When you Install/Start the server it will modify the ```server_config.sii``` automatically and its one way, means if you modify the file manually it will not reflect to WGSM config.
- You can skip step 9 and 10, the plugin will copy automatically for you when you start the server

# Set up
1. Run the game not the server and exit
2. It will Generate ```config.cfg``` on Documents, modifythis with text editor of your choice
3. Modify ```uset g_console "0"``` to 1
4. Modify ```uset g_developer "0"``` to 1
5. Run the game again and Play you must be in the world
6. Open the console interface by pressing “~”  left side of your num 1 key or under ESC key
7. type ```export_server_packages``` then hit enter
8. Exit the game
9. Navigate to your Documents folder you will see ```server_packages.sii``` and ```server_packages.dat``` 
10. Create directory and Paste it to your ```serverfiles\save```
11. Paste your GLST token by pressing EDIT CONFIG button
12. Edit Port and QueryPort except MaxPlayers
13. Start the Server

# License
This project is licensed under the MIT License - see the <a href="https://github.com/ohmcodes/WindowsGSM.AmericanTruckSimulator/blob/main/LICENSE">LICENSE.md</a> file for details

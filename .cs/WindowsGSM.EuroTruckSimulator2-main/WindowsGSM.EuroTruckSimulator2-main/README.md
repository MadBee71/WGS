# WindowsGSM.EuroTruckSimulator2
🧩 WindowsGSM plugin that provides Euro Truck Simulator Dedicated server support!

# WindowsGSM Installation: 
1. Download  WindowsGSM https://windowsgsm.com/ 
2. Create a Folder at a Location you wan't all Server to be Installed and Run.
4. Drag WindowsGSM.Exe into previoulsy created folder and execute it.

# Plugin Installation:
1. Download [latest](https://github.com/ohmcodes/WindowsGSM.EuroTruckSimulator/releases/latest) release
2. Extract then Move **EuroTruckSimulator2.cs** folder to **plugins** folder
3. OR Press on the Puzzle Icon in the left bottom side and install this plugin by navigating to it and select the Zip File.
4. Click **[RELOAD PLUGINS]** button or restart WindowsGSM
5. Navigate "Servers" and Click "Install Game Server" and find "Euro Truck Simulator 2 Dedicated Server [EuroTruckSimulator2.cs]

### Official Documentation
🗃️ https://modding.scssoft.com/wiki/Documentation/Tools/Dedicated_Server

### Unofficial Documentation
🗃️ https://steamcommunity.com/sharedfiles/filedetails/?id=2899373649

### The Game
🕹️ https://store.steampowered.com/app/227300/Euro_Truck_Simulator_2/

### Dedicated server info
🖥️ https://steamdb.info/app/1948160/info/


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
2. It will Generate ```config.cfg``` on Documents, modify this with text editor of your choice
3. Modify ```uset g_console "0"``` to 1
4. Modify ```uset g_developer "0"``` to 1
5. Run the game again and Play (Press Drive) you must be in the world
6. Open the console interface by pressing “~”  left side of your num 1 key or under ESC key
7. type ```export_server_packages``` then hit enter
8. Exit the game
9. Navigate to your Documents folder you will see ```server_packages.sii``` and ```server_packages.dat``` 
10. Create directory and Paste it to your ```serverfiles\save``` (Dont move anything the plugin will do this step for you)
11. Paste your GLST token by pressing EDIT CONFIG button
12. Edit Port and QueryPort except MaxPlayers
13. Start the Server

# Available Start parameters
```
-server_cfg -------------- if you want to place your server_config.sii somewhere
-homedir ----------------- redirects game/server home directory
```

# Server config parameters
```
 lobby_name: "Euro Truck Simulator 2 server"            // Session name, limited to 63 characters.
 description: ""                                        // Session description, limited to 63 characters.
 welcome_message: ""                                    // Session welcome message, limited to 127 characters.
 password: ""                                           // Session password, limited to 63 characters.
 max_players: 8                                         // Maximum players in session, limit is 8 players.
 max_vehicles_total: 100
 max_ai_vehicles_player: 50
 max_ai_vehicles_player_spawn: 50
 connection_virtual_port: 100
 query_virtual_port: 101
 connection_dedicated_port: 27015
 query_dedicated_port: 27016
 server_logon_token: 6544F7E034119F113526E96474F        // Token for game server login (persistent account).
 player_damage: true                                    // Flag if player can receive damage from other players.
 traffic: true                                          // Flag if traffic is enabled.
 hide_in_company: false                                 // Flag if remote player are hidden in company area.
 hide_colliding: true                                   // Flag to hide colliding vehicle after teleport. 
 force_speed_limiter: false                             // Flag to force speed limiter.
 mods_optioning: false                                  // Flag to enable mods marked as optional, to be really optional.
 timezones: 2                                           // Values 0 - 2.
 service_no_collision: false                            // Disable collisions on service area.
 in_menu_ghosting: false                                // Disable collisions when game paused.
 name_tags: true                                        // Show player name tags above vehicles.
 friends_only: false                                    // Not used for dedicated server.
 show_server: true                                      // Not used for dedicated server.
 moderator_list: 2                                      // Default moderators.
 moderator_list[0]: 123456789                           // User steam id.
 moderator_list[1]: 234567891                           // User steam id.
```

# License
This project is licensed under the MIT License - see the <a href="https://github.com/ohmcodes/WindowsGSM.EuroTruckSimulator/blob/main/LICENSE">LICENSE.md</a> file for details

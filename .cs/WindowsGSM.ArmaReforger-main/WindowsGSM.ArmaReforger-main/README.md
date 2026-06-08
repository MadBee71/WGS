# WindowsGSM.ArmaReforger
🧩WindowsGSM plugin that provides ArmaReforger Dedicated server

## PLEASE ⭐STAR⭐ THE REPO IF YOU LIKE IT! THANKS!

### Notes
- If IP Connect works but no Server Listing, try setting your public IP inside the server.json after line 3 bindPort
  - "publicAddress":"your.public.ip.x",
- If local connections work but not External: see chapter Portforwarding
- check the config file listed in Available Params

### WindowsGSM Installation: 
1. Download  WindowsGSM https://windowsgsm.com/ 
2. Create a Folder at a Location you wan't all Server to be Installed and Run.
3. Drag WindowsGSM.Exe into previously created folder and execute it.

### Plugin Installation:
1. Download [latest](https://https://github.com/Raziel7893/WindowsGSM.ArmaReforger/releases/latest) release
2. Either Extract then Move the folder **ArmaReforger.cs** to **WindowsGSM/plugins** 
    1. Press on the Puzzle Icon in the left bottom side and press **[RELOAD PLUGINS]** or restart WindowsGSM
3. Or Press on the Puzzle Icon in the left bottom side and press **[IMPORT PLUGIN]** and choose the downloaded .zip

### Official Documentation
🗃️ https://community.bistudio.com/wiki/Arma_Reforger:Server_Hosting
   https://community.bistudio.com/wiki/Arma_Reforger:Startup_Parameters#Hosting
   https://community.bistudio.com/wiki/Arma_Reforger:Server_Config

### The Game
🕹️ https://store.steampowered.com/app/1874880/Arma_Reforger/

### Dedicated server info
🖥️ https://steamdb.info/app/1874900/info/

### Port Forwarding (YOU NEED THIS, TO BE ABLE TO CONNECT FROM THE INTERNET(only for servers/pcs at home):
- If You don't know How: portforward.com
- 2302 UDP - Default Game Port
- 7777 UDP - Default QueryPort

### Files To Backup
- Save Game 
  - WindowsGSM\servers\%ID%\serverfiles/Saved/
- WindowsGSM Config
  - WindowsGSM\servers\%ID%\configs

### Available Params
check Browse => ServerFiles => Configs/server.json for most of the config posibilities and 
https://community.bistudio.com/wiki/Arma_Reforger:Server_Config

All these params are automatically set by WGSM (but not sure what happens if the argument is passed 2 times
- -bindPort              can be change and working (Change via WGSM settings)
- -a2sPort               can be change and working (Change via WGSM settings)
- -bindIP 	 			 Change via WGSM settings, most games need your local AdapterIP, but if
- -profile \".\\Saved\"  causes your save to be saved localy and not in your appdata



### How can you play with your friends without port forwarding?
- Use [zerotier](https://www.zerotier.com/) folow the basic guide and create network
- Download the client app and join to your network
- Create static IP address for your host machine
- Edit WGSM IP Address to your recently created static IP address
- Give your network ID to your friends
- After they've joined to your network
- They can connect using the IP you've created eg: 10.123.17.1:7777
- Enjoy

### Support
[WGSM](https://discord.com/channels/590590698907107340/645730252672335893)

### Give Love!
[Buy me a coffee](https://ko-fi.com/raziel7893)

[Paypal](https://paypal.me/raziel7893)

### License
This project is licensed under the MIT License - see the <a href="https://github.com/raziel7893/WindowsGSM.ArmaReforger/blob/main/LICENSE">LICENSE.md</a> file for details

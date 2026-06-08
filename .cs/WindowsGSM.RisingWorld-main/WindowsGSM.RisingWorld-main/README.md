# WindowsGSM.Rising World
🧩WindowsGSM plugin that provides Rising World Dedicated server

## To note:
- WGSM will load the unity beta branch as handling JAVA executables is ... a pain. 
- You can change that by modifying the RisingWorld.cs 
  - line 27 AppId to just "339010"
  If that is then still the Java Version, also change
  - line 33 Start Path from "RisingWorldServer.exe" to "rw_server.exe"
  - Your main Java needs to be jre1.8 another java will not work. 

## PLEASE ⭐STAR⭐ THE REPO IF YOU LIKE IT! THANKS!

### WindowsGSM Installation: 
1. Download  WindowsGSM https://windowsgsm.com/ 
2. Create a Folder at a Location you wan't all Server to be Installed and Run.
3. Drag WindowsGSM.Exe into previously created folder and execute it.

### Plugin Installation:
1. Download [latest](https://https://github.com/Raziel7893/WindowsGSM.Rising World/releases/latest) release
2. Either Extract then Move the folder **Rising World.cs** to **WindowsGSM/plugins** 
    1. Press on the Puzzle Icon in the left bottom side and press **[RELOAD PLUGINS]** or restart WindowsGSM
3. Or Press on the Puzzle Icon in the left bottom side and press **[IMPORT PLUGIN]** and choose the downloaded .zip

### Official Documentation
🗃️ https://forum.rising-world.net/thread/11490-dedicated-server-setup-new-version/

### The Game
🕹️ https://store.steampowered.com/app/324080/Rising_World

### Dedicated server info
🖥️ https://steamdb.info/app/339010/

### Port Forwarding (YOU NEED THIS, TO BE ABLE TO CONNECT FROM THE INTERNET(only for servers/pcs at home):
- If You don't know How: Google: YourRouterBrand + Portforwarding ( https://portforward.com/ can also help)
- 4255 TCP+UDP 	- Default Game Port
- 4254 TCP 		- Default Query Port (in theory the queryPort is not needed to be public, but try it if you encounter problems connecting)

### Files To Backup
- Save Gane (You could only save serverfiles/Rising World/Saved , but that includes many big logs)
  - WindowsGSM\servers\%ID%\serverfiles/Worlds
- WindowsGSM Config
  - WindowsGSM\servers\%ID%\configs

### Available Settings
Copy and rename server.example.properties to server.properties and modify the values as you like. 
Following Parameters are always overwritten by WGSM (Click Edit Config Button in WGSM to adjust the values) 
- +Server_Port= 	                  	can be change and working (Change via WGSM settings)
- +Server_QueryPort=                   	can be change and working (Change via WGSM settings)
- +Settings_MaxPlayers 					can be change and working (Change via WGSM settings) 
- And the settings set in Server Start Param 


### Not having an full IPv4 adress ( named CCNAT or DSL Light )
No game or gameserver supports ipv6 only connections. 
- You need to either buy one (most VPN services provide that option. A pal uses ovpn.net for his server, I know of nordvpn also providing that. Should both cost around 7€ cheaper half of it, if your already having an VPN)
- Or you pay a bit more for your internet and take a contract with full ipv4. (depending on your country)
- There are also tunneling methods, which require acces to a server with a full ipv4. Some small VPS can be obtained, not powerfull enough for the servers themself, but only for forwarding. I think there are some for under 5€), the connection is then done via wireguard. but its a bit configuration heavy to setup) 

Or you connect your friends via VPN to your net and play via local lan then.
Many windowsgsm plugin creators recommend zerotier (should be a free VPN designated for gaming) , see chapter below (or tailscale, but no howto there)

## How can you play with your friends without port forwarding?
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
This project is licensed under the MIT License - see the <a href="https://github.com/raziel7893/WindowsGSM.Rising World/blob/main/LICENSE">LICENSE.md</a> file for details

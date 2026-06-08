# WindowsGSM.Wreckfest2
🧩WindowsGSM plugin that provides Wreckfest2 Dedicated server

## PLEASE ⭐STAR⭐ THE REPO IF YOU LIKE IT! THANKS!

### Notes
- Not testet yet, as it needs a license for the gameserver

### WindowsGSM Installation: 
1. Download  WindowsGSM https://windowsgsm.com/ 
2. Create a Folder at a Location you wan't all Server to be Installed and Run.
3. Drag WindowsGSM.Exe into previously created folder and execute it.

### Plugin Installation:
1. Download [latest](https://https://github.com/Raziel7893/WindowsGSM.Wreckfest2/releases/latest) release
2. Either Extract then Move the folder **Wreckfest2.cs** to **WindowsGSM/plugins** 
    1. Press on the Puzzle Icon in the left bottom side and press **[RELOAD PLUGINS]** or restart WindowsGSM
3. Or Press on the Puzzle Icon in the left bottom side and press **[IMPORT PLUGIN]** and choose the downloaded .zip

### Official Documentation
🗃️ Didn't find any documentation yet. Please Let me know if you came accros one

### The Game
🕹️ https://store.steampowered.com/app/1203190/Wreckfest_2/

### Dedicated server info
🖥️ https://steamdb.info/app/3519390/info/

### Port Forwarding (YOU NEED THIS, TO BE ABLE TO CONNECT FROM THE INTERNET(only for servers/pcs at home):
- If You don't know How: portforward.com
- 30100 UDP - Default Game Port
- 27015 UDP - Default QueryPort

### Files To Backup
- Save Gane (You could only save serverfiles/Wreckfest2/Saved , but that includes many big logs)
  - WindowsGSM\servers\%ID%\serverfiles/Wreckfest2/Saved
  - WindowsGSM\servers\%ID%\serverfiles/Wreckfest2/server_config.scnf
- WindowsGSM Config
  - WindowsGSM\servers\%ID%\configs

### Available Params
All these params are automatically set by WGSM
- --save_dir=Saved 	                 


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
This project is licensed under the MIT License - see the <a href="https://github.com/raziel7893/WindowsGSM.Wreckfest2/blob/main/LICENSE">LICENSE.md</a> file for details

### Thanks
Thanks to ohmcodes for the Enshrouded and Palworld Plugins which i used for guidance to create this one

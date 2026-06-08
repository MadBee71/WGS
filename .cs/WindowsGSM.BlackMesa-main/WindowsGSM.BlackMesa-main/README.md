# BlackMesa
🧩 Plugin for WindowsGSM to run a dedicated server for Black Mesa

## PLEASE ⭐STAR⭐ THE REPO IF YOU LIKE IT! THANKS!

### IMPORTANT POINTS, READ FIRST:
- You need to do **Portforwarding**, if you want people outside of your house to be able to connect (look down for it)
- If you want your Server to be Listed: (default is LAN/IP-Connect ONLY!)
  - Link the Server to your Account: Edit Config => **Server GSLT** (https://steamcommunity.com/dev/managegameservers App ID )
  - **install the SteamClient( START IT ONCE**, no login, no autostart, just install and start it once)
  - go to the Server.cfg (WindowsGSM Click on **Browse => Server Files**, then go **\bms\cfg** and open **server.cfg** with Editor)
    - Excange "sv_lan" from 1 to  (lan/ip connections will still work in addition)
- The Server can not be **connected from the same Machine**. You will need to create the server within Black Mesa game (Multiplayer => create) if one Machine has to act as Server AND Client.

### WindowsGSM Installation: 
1. Download  WindowsGSM https://windowsgsm.com/ 
2. Create a Folder at a Location you wan't all Server to be Installed and Run.
3. Drag WindowsGSM.Exe into previously created folder and execute it.

### Plugin Installation:
1. Download [latest](https://github.com/Raziel7893/WindowsGSM.BlackMesa/releases/latest) release
2. Either Extract then Move the folder **BlackMesa.cs** to **WindowsGSM/plugins** 
    1. Press on the Puzzle Icon in the left bottom side and press **[RELOAD PLUGINS]** or restart WindowsGSM
3. Or Press on the Puzzle Icon in the left bottom side and press **[IMPORT PLUGIN]** and choose the downloaded .zip

### Configuration
Modify the server config file to adjust settings(Brose=>Server Files): serverfiles\bms\cfg

### Official Documentation
🗃️ Didn't find any documentation yet. Please Let me know if you came accros one

### The Game
🕹️ https://store.steampowered.com/app/362890/Black_Mesa/

### Dedicated server info
🖥️ https://steamdb.info/app/346680/info/

### Port Forwarding (YOU NEED THIS, TO BE ABLE TO CONNECT FROM THE INTERNET(only for servers/pcs at home, Hosted Servers work different there, make sure you have a full IPv4 and if your provider needs explicit portforwarding):
- If You don't know How: Google: YourRouterBrand + Portforwarding
- 27015 UDP - **Default Game Port**(look in the 7th column in WindowsGSM), 
  - this one is needed to be forwarded in your Router!
- 27015 TCP - **QueryPort** default, in theory the QueryPort should not be needed as PortForward

### Connecting to a LAN Server:
- Go to Opptions
- Enable Developer Mode
- Go To Keyboard Bindings and set the Key for the Console
- go Back
- Press that key and enter
  - connect IP_OF_THE_SERVER:GamePort (if port is 27015 you can leave it out)
  - if your in the same network use the local IP of that server, else use you onlineIP
  - https://www.whatismyip.com/  

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
This project is licensed under the MIT License - see the <a href="https://github.com/raziel7893/WindowsGSM.BlackMesa/blob/main/LICENSE">LICENSE.md</a> file for details

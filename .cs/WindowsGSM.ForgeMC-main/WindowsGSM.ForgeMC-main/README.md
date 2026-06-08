# WindowsGSM.ForgeMC

## Requirements
[WindowsGSM](https://github.com/WindowsGSM/WindowsGSM) >= 1.21.0

## Installation
1. Download the Plugin via the green Download Code Button or use [latest](https://github.com/Raziel7893/WindowsGSM.ForgeMC/releases/latest) release if available
1. Move the **ForgeMC.cs** folder to **plugins** folder
1. Click the **[RELOAD PLUGINS]** button or restart WindowsGSM

## License
The Project was re-forked from ada64bit, as the project seems inactive with a broken release link from day one. My pullrequest was also unanswered. 

This project, excluding any `**/*.png` files, is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details.

[ForgeMC.cs/author.png](ForgeMC.cs/author.png) is created by [@taiyaki_tv](https://twitter.com/taiyaki_tv) and reserved for use by me, dwhitacre.

[ForgeMC.cs/ForgeMC.png](ForgeMC.cs/ForgeMC.png) is licensed by [MinecraftForge](https://github.com/MinecraftForge/MinecraftForge).

### Not having an full IPv4 adress ( named CCNAT or DSL Light )
Allthough Minecraft can accept ipv6 only connections, all your clients also need an ipv6 adapter. to run MC the "normal" way (for IPv6 only, see below):
- You need to either buy one (most VPN services provide that option. A pal uses ovpn.net for his server, I know of nordvpn also providing that. Should both cost around 7€ cheaper half of it, if your already having an VPN)
- Or you pay a bit more for your internet and take a contract with full ipv4. (depending on your country)
- There are also tunneling methods, which require acces to a server with a full ipv4. Some small VPS can be obtained, not powerfull enough for the servers themself, but only for forwarding. I think there are some for under 5€), the connection is then done via wireguard. but its a bit configuration heavy to setup) 

Or you connect your friends via VPN to your net and play via local lan then.
Many windowsgsm plugin creators recommend zerotier (should be a free VPN designated for gaming) , see chapter at the end (or tailscale, but no howto there)


## IPv6 only
If you want, or need to because you don't get an IPv4 from your Provider, you can also start minecraft as IPv6 only server. There are a few steps necesarry for that to work:
- Disable Privacy Extensions on the host computer
  - open windows powershell and type these commands
    - Set-NetIPv6Protocol -RandomizeIdentifiers Disabled
    - Set-NetIPv6Protocol -UseTemporaryAddresses Disabled
    - Restart-Computer
- Edit run.bat (mark the server in WindowsGSM, click on Browse => Server Files, there it should be)
- Add the following parameters to the Java execution line:
  - -Djava.net.preferIPV4stack=false
  - -Djava.net.preferIPv6Addresses=true
- You still need to confiure your router to allow the connections:
- Port forwarding/Firewall on the router with port 25565 UDP and TCP(the ones listed in the file server.properties)
- The firewall on the host-pc also needs to be manually be configured to open Port 25565 TCP and UDP Inbound (normaly WindowsGSM does this, but as StartPath is the the file that executes(java.exe), it could be that it fails to do so)
In some Cases: 
- in some cases the creator had to open a specific filter on my router to allow ICMP traffic
  - In my case (FritzBox 7360) : Internet -> Filter -> List
- steps are copied from : https://www.reddit.com/r/admincraft/comments/zcab6h/connecting_to_a_minecraft_server_with_ipv6/

Connecting to an IPv6only Server:
- your players all need a ipv6 stack enabled, but there is one integrated in windows, you just need to activate it:
  - https://answers.microsoft.com/en-us/windows/forum/all/enable-teredo-ipv6/62524566-6ee2-4690-bc16-238a2d205e3b
- connect with [IPv6]:25565 , the ipv6 address has to be written in brackets.

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

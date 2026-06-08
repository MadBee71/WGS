# WindowsGSM.Terraria
🧩 WindowsGSM plugin for supporting Terraria

## Modding
- If you want to use TShock, extract in serverviles to the folder tshock. so the Tshop server path should be serverfiles/tshock/TShock.Server.exe
- if it doesn't work run TShock.Installer once first
- If you want to use another modloader just edit the ModExeName in Terraria.cs file in your plugins folder at line 37 
- https://github.com/Pryaxis/TShock/releases

## Requirements
[WindowsGSM](https://github.com/WindowsGSM/WindowsGSM) >= 1.21.0

.NET Framework 4.0 (or greater) installed

XNA Framework 4.0 installed (you can get [Here](https://web.archive.org/web/20201222035408if_/https://download.microsoft.com/download/A/C/2/AC2C903B-E6E8-42C2-9FD7-BEBAC362A930/xnafx40_redist.msi))

## Installation
1. Move **Terraria.cs** folder to **plugins** folder
1. Click **[RELOAD PLUGINS]** button or restart WindowsGSM

## Additional Command Line options

Full list [Here](https://terraria.gamepedia.com/Server)

<ul><li><code>-config &lt;file path&gt;</code> - Specifies a configuration file to use.</li>
<li><code>-port &lt;number&gt;</code> - Specifies the port to listen on.</li>
<li><code>-players &lt;number&gt; / -maxplayers &lt;number&gt;</code> - Sets the max number of players.</li>
<li><code>-pass &lt;password&gt; / -password &lt;password&gt;</code> - Sets the server password.</li>
<li><code>-motd &lt;text&gt;</code> - Set the server motto of the day text.</li>
<li><code>-world &lt;file path&gt;</code> - Load a world and automatically start the server.</li>
<li><code>-autocreate &lt;number&gt;</code> - Creates a world if none is found in the path specified by -world. World size is specified by: 1(small), 2(medium), and 3(large).</li>
<li><code>-banlist &lt;file path&gt;</code> - Specifies the location of the banlist. Defaults to "banlist.txt" in the working directory.</li>
<li><code>-worldname &lt;world name&gt;</code> - Sets the name of the world when using -autocreate.</li>
<li><code>-secure</code> - Adds additional cheat protection to the server.</li>
<li><code>-noupnp</code> - Disables automatic universal plug and play.</li>
<li><code>-steam</code> - Enables Steam support.</li>
<li><code>-lobby friends / -lobby private</code> - Allows only friends to join the server or sets it to private if Steam is enabled.</li>
<li><code>-ip &lt;ip address&gt;</code> - Sets the IP address for the server to listen on</li>
<li><code>-forcepriority &lt;priority&gt;</code> - Sets the process priority for this task. If this is used the "priority" setting below will be ignored.</li>
<li><code>-disableannouncementbox</code> - Disables the text announcements Announcement Box makes when pulsed from wire.</li>
<li><code>-announcementboxrange &lt;number&gt;</code> - Sets the announcement box text messaging range in pixels, -1 for serverwide announcements.</li>
<li><code>-seed &lt;seed&gt;</code> - Specifies the world seed when using -autocreate <span id="serverconfig"></span></li></ul>

### Files To Backup
- Save Gane (You could only save serverfiles/SMALLAND/Saved , but that includes many big logs)
  - WindowsGSM\servers\%ID%\serverfiles/Worlds
- WindowsGSM Config
  - WindowsGSM\servers\%ID%\configs
 
## Not having an full IPv4 adress ( named CCNAT or DSL Light )
No game or gameserver supports ipv6 only connections. 
- You need to either buy one (most VPN services provide that option. A pal uses ovpn.net for his server, I know of nordvpn also providing that. Should both cost around 7€ cheaper half of it, if your already having an VPN)
- Or you pay a bit more for your internet and take a contract with full ipv4. (depending on your country)
- There are also tunneling methods, which require acces to a server with a full ipv4. Some small VPS can be obtained, not powerfull enough for the servers themself, but only for forwarding. I think there are some for under 5€), the connection is then done via wireguard. but its a bit configuration heavy to setup) 

Or you connect your friends via VPN to your net and play via local lan then.
Many windowsgsm plugin creators recommend zerotier (should be a free VPN designated for gaming) , see chapter below (or tailscale, but no howto there)

### How can you play with your friends without port forwarding?
- Use [zerotier](https://www.zerotier.com/) folow the basic guide and create network
- Download the client app and join to your network
- Create static IP address for your host machine
- Edit WGSM IP Address to your recently created static IP address
- Give your network ID to your friends
- After they've joined to your network
- They can connect using the IP you've created eg: 10.123.17.1:7777
- Enjoy

## Support
[WGSM](https://discord.com/channels/590590698907107340/645730252672335893)

## Give Love!
[Buy me a coffee](https://ko-fi.com/raziel7893)

[Paypal](https://paypal.me/raziel7893)

## License
This project is licensed under the MIT License - see the [LICENSE.md](https://github.com/BattlefieldDuck/WindowsGSM.ARMA3/blob/master/LICENSE) file for details

## Thanks
Thanks to kickbut101 for base plugin


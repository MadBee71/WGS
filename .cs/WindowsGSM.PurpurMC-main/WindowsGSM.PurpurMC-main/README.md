# WindowsGSM PurpurMC Plugin

A plugin for adding, creating, and managing **Purpur** Minecraft servers in WindowsGSM.  

---

## Features

- Install new server: downloads latest `.jar`, creates `eula.txt`, generates `server.properties`  
- Start / Stop / Restart server with embedded console and RCON
- Query port support for status and players  
- Update server automatically to latest build  
- Import existing server without overwriting configs
- Detects any `purpur*.jar` file and enables WindowsGSM management features.  
- Managability of server and plugin config files  

---

## Installation

1. Place /PurpurMc.cs/ folder containing files `PurpurMC.cs`, `icon.png`, `author.png` files in `WindowsGSM/plugins/`.  
2. Reload the plugins WindowsGSM or restart WindowsGSM if they do not appear.  
3. Enable `Embed Console` option to see the server console output in the WindowsGSM manager.  

---

## Usage

- Create new or import existing Purpur server
- Edit server config:
    RAM settings are default 8 GB of RAM (-Xms8G -Xmx8G)
    Players max set to 50
    World name is default MC `world`
- Manage server via WindowsGSM buttons, console, or RCON  
- Update server via WindowsGSM; plugin fetches latest `.jar`  

---

## License

MIT License, credit **estvn_ca** required. See `LICENSE` file.

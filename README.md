<div align="center">
  <img src="wgs.png" alt="Windows Game Server" width="320"/>
  <h1>Windows Game Server</h1>
  <p><strong>Single-window management panel for Windows game servers</strong></p>

  ![Version](https://img.shields.io/github/v/release/MadBee71/WGS?label=version&color=blue)
  ![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
  ![Platform](https://img.shields.io/badge/platform-Windows-0078D4?logo=windows)
  ![License](https://img.shields.io/badge/license-MIT-green)
  ![Games](https://img.shields.io/badge/supported_games-101+-orange)
  ![Build](https://img.shields.io/badge/build-passing-brightgreen)

  ðŸŒ **[wgsserver.com](https://wgsserver.com)** â€” website, full game list, installation guide & user manual

</div>

---

## What is Windows Game Server?

**Windows Game Server (WGS)** is a free, open-source desktop application that lets you host and manage dedicated game servers on any Windows PC â€” without touching the command line. It's a modern, actively maintained alternative to WindowsGSM, AMP (CubeCoders) and Pterodactyl Panel for anyone who wants a native Windows experience. See [wgsserver.com](https://wgsserver.com) for the full feature list, supported games, and documentation.

Instead of juggling SteamCMD scripts, batch files, Task Scheduler entries and manual firewall rules, WGS brings everything into one clean window:

- **Install** any supported game server in one click â€” SteamCMD is downloaded and run automatically in the background
- **Start, stop and restart** servers with a single button â€” or let WGS do it automatically after a crash, with smart crash-loop detection
- **Monitor** CPU and RAM usage per server in real time, with history graphs and a global system dashboard
- **Schedule** automatic restarts, updates and backups at any time of day or week
- **Back up** world saves and configs automatically before every update, with configurable retention
- **Send console commands** directly from the UI â€” no need to switch windows or open a terminal
- **Edit config files** for any server directly inside WGS, without opening a file manager
- **Install Workshop mods** and manage Oxide/Minecraft plugins from the same interface
- **Add any game** that isn't built-in using the graphical Plugin Creator â€” no coding required
- **Control servers remotely** via Discord bot commands or the built-in REST API
- **Manage firewall rules** automatically â€” WGS opens and closes the right ports when servers start and stop

WGS is designed for home lab hosts, small community server admins and anyone who wants a clean, reliable way to keep game servers running on Windows without spending time on maintenance.

Built with the help of AI coding tools, with every feature driven, tested and decided by the author.

---

## ðŸ“· Screenshot

<p align="center">
  <img src="screenshot.png" width="800">
</p>

---

> [!IMPORTANT]
> **Windows SmartScreen Warning:**
>
> Since WGS is an independent open-source tool that manages system-level tasks (Firewall, Process Priorities), Windows might show a "SmartScreen" warning.
> To run WGS: Right-click `WindowsGameServer.exe` â†’ **Properties** â†’ Check **Unblock** at the bottom â†’ **OK**.

---

## âœ¨ Features

### Server management
| Feature | Description |
|---|---|
| ðŸŽ® **101+ supported games** | Ready-made plugins for the most popular game servers |
| â¬‡ï¸ **SteamCMD integration** | Install and update servers with one click â€” SteamCMD downloaded automatically |
| ðŸ”„ **Auto restart** | Automatic restart after crash, with configurable delay and crash loop detection |
| ðŸ” **Auto-update** | Periodic SteamCMD updates on a configurable interval while the server runs |
| â§‰ **Server cloning** | Duplicate any server with all settings â€” ports assigned automatically |
| ðŸ’¤ **Wake-on-demand** | Server starts automatically when the first player connects, saving resources when idle |
| ðŸ˜´ **Shut down when empty** | Server stops automatically after a configurable idle timeout when all players leave |

### Monitoring
| Feature | Description |
|---|---|
| ðŸ“Š **System dashboard** | Global CPU, RAM and disk usage across all running servers |
| ðŸ“ˆ **Per-server performance charts** | CPU and RAM history graphs up to 1 hour, with selectable time range |
| ðŸ‘¥ **Player statistics** | Session tracking and total playtime per player, stored in SQLite |
| ðŸ“ˆ **Activity heatmap & top players** | Hourly activity chart (useful for scheduling restarts during quiet hours) and a most-active-players-in-30-days view |
| ðŸŒ **Bandwidth & connections** | Live network in/out and active connection count per server |
| âš ï¸ **Crash prediction** | Warns before a likely crash from RAM growth, sustained high CPU or memory leaks â€” or switch to a simpler "low system memory only" mode with a configurable threshold |

### Automation
| Feature | Description |
|---|---|
| ðŸ—“ï¸ **Task scheduler** | Schedule start, stop, restart, update, backup, or a saved Quick Command â€” once, daily, weekly, or on a repeating "every X minutes/hours" interval |
| ðŸ’¾ **Automatic backups** | Zip backups of world saves before updates, retention by count and/or age with a manual cleanup option, optional incremental backups for large worlds, selective backup paths per server â€” scheduled backups are skipped automatically if the server hasn't run in the last 24 hours |
| ðŸ”— **Group ban-list sync** | Ban a player on one server and it's automatically applied to every other running server in the same group (same game) â€” and replayed on group servers that were offline when the ban happened |
| ðŸ“‹ **Shareable status page** | A read-only, no-login link per server showing live player count and uptime â€” safe to post in Discord or on a website |
| ðŸ“œ **Log-based crash detection** | Scans console output for known crash-precursor patterns (out-of-memory, fatal errors, access violations) in addition to the CPU/RAM heuristics |
| ðŸ“¢ **Restart warnings** | Players get an in-game warning before daily, scheduled or auto-update restarts â€” works for Rust, Source-engine games, Minecraft, ARK, Palworld, DayZ and 7 Days to Die |

### Remote access
| Feature | Description |
|---|---|
| ðŸ“Ÿ **RCON console** | Send commands to running servers via Source RCON protocol |
| ðŸ¤– **Discord bot** | Control servers from any Discord channel: `!start`, `!stop`, `!restart`, `!update`, `!backup`, `!cmd` â€” plus an optional live status board with a "Wake" button, and admin control buttons on a separate restricted channel |
| ðŸŒ **REST API & web dashboard** | Built-in HTTP server for external integrations â€” start/stop/status/metrics/backup/restore endpoints, plus a sortable browser dashboard with a live per-server CPU graph |
| ðŸ–¥ï¸ **Remote machine support** | Manage servers running on other PCs from a single master panel |

### Notifications
| Feature | Description |
|---|---|
| ðŸ”” **Discord webhooks** | Get notified on start, stop, crash, update and player join/leave events in Discord â€” global or per-server webhook URL |
| ðŸ“§ **Email notifications (SMTP)** | Receive the same alerts by email â€” configurable per server |

### Configuration & mods
| Feature | Description |
|---|---|
| ðŸ“ **Config editor** | Browse and edit any server config file directly inside WGS, with automatic version history and one-click restore |
| ðŸ—‚ï¸ **Steam Workshop** | Install and manage Workshop mods for supported games via SteamCMD, with a live title preview when entering an item ID |
| ðŸ“ **File manager** | Browse, upload, download and delete server files without leaving WGS |

### System & extensibility
| Feature | Description |
|---|---|
| ðŸ›¡ï¸ **Firewall management** | Windows Firewall rules opened/closed automatically on start and stop |
| ðŸ§¹ **Server hygiene** | Scans for and cleans up leftover log files, crash report folders and stray temp files while a server is stopped |
| âš¡ **Quick commands** | Save up to a handful of console command shortcuts (e.g. a welcome message) as one-click buttons |
| âš™ï¸ **CPU affinity, priority & RAM limit** | Per-server core pinning, process priority and hard RAM cap via Windows Job Objects |
| ðŸ”§ **Custom Plugin Creator** | Graphical tool to add any game server â€” no code required, and remove ones you've created when you no longer need them |
| ðŸ“¦ **Plugin import / export** | Share plugins as `.cs` files between machines |
| ðŸ”” **System tray** | Runs minimised in the background with tray notifications |
| ðŸ”’ **Encrypted credentials** | Steam login and Discord tokens encrypted with Windows DPAPI |

---

## ðŸŽ® Supported Games

101+ games supported out of the box â€” including Valheim, Rust, CS2, ARK, DayZ, Palworld, Minecraft, and many more.

ðŸ‘‰ **[Full game list with search â†’](https://wgsserver.com/docs/games.html)**

The **Custom Plugin Creator** lets you add any other game server without touching code.

---

## ðŸ–¥ï¸ Requirements

- **Windows 10 / Windows Server 2019** or newer
- **.NET 10 Runtime** — [download here](https://dotnet.microsoft.com/download/dotnet/10.0)
- **SteamCMD** â€” downloaded automatically on first install
- Administrator rights for firewall rule management

---

## ðŸš€ Installation

### Pre-built binary (recommended)

1. Download the latest release from the [Releases](../../releases) page
2. Extract the zip to a folder of your choice
3. Run `WindowsGameServer.exe`
4. If you get a .NET error, install the [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)

### Build from source

```bash
git clone https://github.com/MadBee71/WGS.git
cd WindowsGameServer/WGS
dotnet publish -c Release -o publish
```

> Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

---

## ðŸ“¦ Project structure

```
WGS/
â”œâ”€â”€ Games/              # Game plugins (IGamePlugin interface)
â”‚   â”œâ”€â”€ GamePluginBase.cs
â”‚   â”œâ”€â”€ GameRegistry.cs
â”‚   â”œâ”€â”€ ValheimPlugin.cs
â”‚   â”œâ”€â”€ RustPlugin.cs
â”‚   â””â”€â”€ ...             # One .cs per game
â”œâ”€â”€ Models/             # Data models (GameServer, ConsoleMessage...)
â”œâ”€â”€ Services/           # Business logic and background services
â”œâ”€â”€ ViewModels/         # MVVM ViewModels
â”œâ”€â”€ Views/              # WPF XAML views
â””â”€â”€ publish/            # Published executable output
```

---

## ðŸ”Œ Adding a custom plugin

### Graphical Plugin Creator

WGS includes a built-in Plugin Creator tool:
1. Open **Tools â†’ Plugin Creator**
2. Fill in the game details (name, Steam AppID, executable, ports...)
3. Click **Save** â€” the plugin appears in the game list immediately

You can also export any plugin to a `.cs` file and share it, or import one from another machine via **Tools â†’ Import Plugin**.

### Writing a plugin in code

Create a new file `Games/MyGamePlugin.cs`:

```csharp
using WGS.Games;
using WGS.Models;

public class MyGamePlugin : GamePluginBase
{
    public override string GameId            => "mygame";
    public override string GameName          => "My Game";
    public override string Description       => "Short description";
    public override string Category          => "Survival";
    public override int    SteamAppId        => 123456;
    public override string Executable        => "server.exe";
    public override int    DefaultPort       => 7777;
    public override int    DefaultQueryPort  => 27015;
    public override int    DefaultMaxPlayers => 32;

    public override string BuildStartArguments(GameServer s)
        => $"-port {s.ServerPort} -queryport {s.QueryPort} -maxplayers {s.MaxPlayers}";
}
```

Register it in `Games/GameRegistry.cs`:

```csharp
Register(new MyGamePlugin());
```

---

## ðŸ—ï¸ Architecture

```
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚            WPF UI (XAML)            â”‚
â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¬â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
â”‚  MainViewModel â”‚  ServerViewModel   â”‚  â† CommunityToolkit.Mvvm
â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”´â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
â”‚  ServerManagerService               â”‚  â† Process lifecycle
â”‚  SteamCmdService                    â”‚  â† Install / update / Workshop
â”‚  BackupService                      â”‚  â† Zip backups + retention
â”‚  FirewallService                    â”‚  â† netsh / Windows Firewall COM
â”‚  RconService                        â”‚  â† Source RCON protocol
â”‚  SystemMetricsService               â”‚  â† Global CPU / RAM / disk
â”‚  PerformanceMonitorService          â”‚  â† Per-process CPU / RAM
â”‚  PerfHistoryService                 â”‚  â† Time-series chart data
â”‚  PlayerStatsService                 â”‚  â† Session tracking (SQLite)
â”‚  ModManagerService                  â”‚  â† Oxide / Minecraft plugins
â”‚  SteamWorkshopService               â”‚  â† Workshop item management
â”‚  ConfigEditorService                â”‚  â† In-app config file editing
â”‚  ScheduledTaskService               â”‚  â† Recurring automation tasks
â”‚  NotificationService                â”‚  â† Discord webhooks
â”‚  DiscordBotService                  â”‚  â† Discord bot (long-poll)
â”‚  WebApiService                      â”‚  â† REST API (HttpListener)
â”‚  ServerGroupService                 â”‚  â† Server grouping
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
         â”‚
         â–¼
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚  IGamePlugin (per game)             â”‚
â”‚  GamePluginBase (defaults)          â”‚
â”‚  GameRegistry (registration)        â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
```


---

## ðŸ¤ Contributing

Pull requests are welcome! For large changes, please open an issue first to discuss what you'd like to change.

1. Fork this repository
2. Create a feature branch: `git checkout -b feature/my-new-feature`
3. Commit your changes: `git commit -m "Add: my new feature"`
4. Push: `git push origin feature/my-new-feature`
5. Open a Pull Request

---

## ðŸ“„ License

MIT License â€” see the [LICENSE](LICENSE) file.

---

## Support

The biggest help is completely free: if WGS is useful to you, **â­ star this repo** â€” it's the main way other people find the project.

If you'd also like to chip in financially, that's entirely optional and never expected â€” it goes toward keeping development going, not "buying" features or support:

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/madbee71)

<div align="center">
  <sub>Built with .NET 10 Â· WPF Â· CommunityToolkit.Mvvm</sub>
</div>


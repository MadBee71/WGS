# WindowsGSM.FabricMC
🧩 WindowsGSM plugin for supporting Minecraft: Fabric Servers

## Requirements
[WindowsGSM](https://github.com/WindowsGSM/WindowsGSM) >= 1.21.0

## Installation
1. Download the [latest](https://github.com/chromamods/WindowsGSM.FabricMC/releases/latest) release
1. Move the **FabricMC.cs** folder to **plugins** folder
1. Click **[RELOAD PLUGINS]** button or restart WindowsGSM

## Notes

Once a server is installed, be sure to change the launch arguments WITHIN WindowsGSM otherwise the server will launch without arguments. 

## Known issues

1. Getting the local and latest build information is currently unavailable. For now, these options are rigged.
1. Clicking 'kill' within WindowsGSM does not fully kill the server. If you accidentally click this, open Task Manager and find 'java.exe', then force close it that way. 
1. The import feature of WindowsGSM does not work, as the plugin relies on a batch file for starting/updating the server. Attempting an import will likely lead to an error.  To import servers, copy the critical files manually for now.

## Contributing
Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.

## License
[MIT](https://choosealicense.com/licenses/mit/)

# WindowsGSM.Sunkenland
🧩WindowsGSM plugin that provides Sunkenland Dedicated server


# WindowsGSM Installation: 
1. Download  WindowsGSM https://windowsgsm.com/ 
2. Create a Folder at a Location you wan't all Server to be Installed and Run.
4. Drag WindowsGSM.exe into previously created folder and execute it.

# Plugin Installation:
1. Download [latest](https://github.com/ohmcodes/WindowsGSM.Sunkenland/releases/latest) release
2. Extract then Move **Sunkenland.cs** folder to **plugins** folder
3. OR Press on the Puzzle Icon in the left bottom side and install this plugin by navigating to it and select the Zip File.
4. Click **[RELOAD PLUGINS]** button or restart WindowsGSM
5. Navigate "Servers" and Click "Install Game Server" and find "Sunkenland Dedicated Server [Sunkenland.cs]

# First Start Steps:
- The server can not create it's own world, and therefore there are a few steps to start using that. (**Check Creating a World** paragraph)
  - Go then to WindowsGSM, click on Edit Config and set the World ID to the **Server Start Map** field (you find the ID here in the Foldername (everything after ~: %AppData%\..\LocalLow\Vector3 Studio\Sunkenland\Worlds)
  - looks like that 98fb3e39-3d7f-4ec0-ad04-1a0a069ff810
- The Ports can not be changed, as the sunkenland devs did not implement that yet
- It seems, that currently Steam is needed to be installed on the same Machine, and also be run and logged in, as it makes a license check on startUp.
- If you want to connect to your own server on the same PC:
  -   go to your steam Library, rightclick on sunkenland, browse local files and execute the Sunkenland.exe manually. you apparently can not run Sunkenland via steam while the dedicated server is running on the same machine
- For Further References look at the PDF provided in the Server Files 

# Creating a World:
1. Before running the dedicated server, ensure you have a valid, previously created
world save file. You can generate one using the Sunkenland game client and
then transfer it to your dedicated server machine. **If you run the GameClient on the same machine, skipp the rest and add the guid to WindowsGSM**
2. For Windows users, locate your save files in: (just enter this in a Explorer Window)
%AppData%\..\LocalLow\Vector3 Studio\Sunkenland\Worlds
Within the “Worlds” folder, you can find all the worlds you've created, if any.
3. Copy the specific world you wish to host and paste it into the same directory on
the dedicated server machine. Note: If you do not find any of these folders in the
dedicated server machine, you need to manually create all necessary folders to
match the path mentioned above. (e.g. if you have never played Sunkenland on a
Windows PC, you need to create “Vector3 Studio”, “Sunkenland” and “Worlds”
folder by yourself.)

### Official Documentation
🗃️ https://www.sunkenlandgame.com/post/dedicated-server-user-manual

### Unofficial Documentation
🗃️ None

### The Game
🕹️ https://store.steampowered.com/app/2080690/Sunkenland/

### Dedicated server info
🖥️ https://steamdb.info/app/2667530/info/


# License
This project is licensed under the MIT License - see the <a href="https://github.com/ohmcodes/WindowsGSM.Sunkenland/blob/main/LICENSE">LICENSE.md</a> file for details

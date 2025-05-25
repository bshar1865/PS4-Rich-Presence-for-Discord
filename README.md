# PS4 Rich Presence for Discord

This is a forked version of the original PS4 Rich Presence for Discord application, improved Windows integration.

## Credits
This application is based on the work by zorua. All credit for the original concept and implementation goes to them.

## Changes in this Fork
- Windows-native WPF application with proper system tray support
- Single instance application handling
- Improved connection management
- Automatic PS4 detection
- English-only interface
- Modern UI with better user experience

## Features
- Display your PS4 gaming status on Discord
- Automatic PS4 detection on your network
- Minimize to system tray
- Edit game information
- Customizable update intervals
- Hibernate mode support
- Support for PS4, PS2, and PS1 games

## Requirements
- Windows 10/11
- .NET 8.0 or later
- PS4 with GoldHEN installed
- Discord desktop app
- Both PS4 and PC must be on the same network

## Installation
1. Download the latest release
2. Run the application
3. Your PS4 will be detected automatically, or you can manually enter its IP address
4. The application will minimize to system tray and update your Discord status

## Usage
- Right-click the tray icon to access options
- Use the Settings dialog to configure connection settings
- Use Edit Game Info to customize game information
- The application will automatically maintain your Discord presence

## Building from Source

### Prerequisites
1. Install [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
2. Install [Visual Studio 2022](https://visualstudio.microsoft.com/) (optional, if you prefer an IDE)

### Building with Visual Studio
1. Open `PS4RichPresence.csproj` in Visual Studio 2022
2. Select `Release` configuration and `x64` platform
3. Build Solution (F6)
4. The output will be in `bin\Release\net8.0-windows\win-x64`

### Building with .NET CLI
1. Open a terminal in the project directory
2. Run the following commands:
```powershell
# Clean previous builds
dotnet clean

# Restore NuGet packages
dotnet restore

# Build and publish as single file
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
3. The output will be in `bin\Release\net8.0-windows\win-x64\publish`

The published application will be a single executable file that includes all dependencies.

## License
This project maintains the same license as the original repository by Tustin.

## Contributing
Feel free to submit issues and pull requests to the repository.

## Display Example
No game 	| 	PS4 game 	|	PS2 game* 	|	PS1 game* 	|
 -----------|---------------|---------------|---------------|
 ![noGame](https://i.imgur.com/MTrBFew.png) | ![PS4Game](https://i.imgur.com/gtIW76h.png) | ![PS2Game](https://i.imgur.com/riihpST.png) 	| ![PS1Game](https://i.imgur.com/CRRjGFZ.png) 	|  
 
\* PS2 and PS1 will only have custom game covers if you manually upload or [change](https://github.com/zorua98741/PS4-Rich-Presence-for-Discord/wiki#changing-image) the default 

## Warning
```
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Known Issues/Limitations
- Putting the PS4 into rest mode or disconnecting it from the internet and then turning it back on/reconnecting it can cause the FTP server to not respond.
  To fix, disable and re-enable the FTP server. (PS4 limitation)
- No mobile support or way to run without a PC (Discord limitation)
- If the user changes the NP Title of a game (or it is incorrect by default), then the presence will use whatever the user changed it to, making the presence display the wrong game (PS4(?) limitation)

## Contact
You can contact me via Discord: bshar1865

## Additional Information
[Wiki](https://github.com/zorua98741/PS4-Rich-Presence-for-Discord/wiki)

## Acknowledgments
- [ORBISPatches](https://orbispatches.com/) and 0x199 for pointing me in the direction of using the tmdb api
- [PS2 games.md](https://github.com/zorua98741/PS4-Rich-Presence-for-Discord/blob/main/PS2%20games.md) sourced from [Veritas83](https://github.com/Veritas83/PS2-OPL-CFG/blob/master/test/PS2-GAMEID-TITLE-MASTER.csv)
- [PS1 games.md](https://github.com/zorua98741/PS4-Rich-Presence-for-Discord/blob/main/PS1%20games.md) sourced from [CRX](https://psxdatacenter.com/information.html)  
- [Tustin](https://github.com/Tustin/PlayStationDiscord-Games/blob/master/script.py) for their tmdb hash code

---
[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/N4N87V7K5)

# PS4 Rich Presence for Discord

This is a forked and fully rewritten version of the original PS4 Rich Presence for Discord, now implemented in C# with a modern Windows-native GUI. The original was a Python-based CLI tool without a graphical interface.

## Changes in this Fork
- Complete rewrite in C# using WPF
- Windows-native GUI with modern design
- System tray integration
- Single-instance application handling
- Improved and more stable PS4 connection handling
- Automatic PS4 detection over the network
- English-only interface
- Custom game info editing
- Support for PS4, PS2, and PS1 game presence

## Features
- Display your PS4 gaming status on Discord
- Auto-detects PS4 on the network
- Minimize to system tray
- Manually or automatically update game information
- Customizable update intervals
- Hibernate mode support
- Compatible with PS4, PS2, and PS1 games

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
dotnet clean
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
3. The output will be in `bin\Release\net8.0-windows\win-x64\publish`

## License
This project is licensed under the MIT License with permission from the original author of the Python version.

## Contributing
Feel free to submit issues or pull requests.

## Display Example
No game 	| 	PS4 game 	|	PS2 game* 	|	PS1 game* 	|
 -----------|---------------|---------------|---------------|
 ![noGame](https://i.imgur.com/MTrBFew.png) | ![PS4Game](https://i.imgur.com/gtIW76h.png) | ![PS2Game](https://i.imgur.com/riihpST.png) | ![PS1Game](https://i.imgur.com/CRRjGFZ.png) |

* PS2 and PS1 will only have custom game covers if you manually upload or [change](https://github.com/zorua98741/PS4-Rich-Presence-for-Discord/wiki#changing-image) the default

## Known Issues / Limitations
- Putting the PS4 into rest mode or disconnecting it from the internet and then turning it back on/reconnecting it can cause the FTP server to not respond. To fix this, disable and re-enable the FTP server.
- No mobile support or way to run without a PC (Discord limitation)
- If the NP Title ID is modified or incorrect, the wrong game may show up in Discord (PS4 limitation)

## Contact
You can contact me on Discord: bshar1865

## Additional Information
[Wiki](https://github.com/zorua98741/PS4-Rich-Presence-for-Discord/wiki)

## Acknowledgments
- zorua – for the original Python-based CLI implementation [GitHub](https://github.com/zorua98741/PS4-Rich-Presence-for-Discord)
- [ORBISPatches](https://orbispatches.com/) and 0x199 – for pointing me toward using the TMDB API
- [PS2 games.md](https://github.com/zorua98741/PS4-Rich-Presence-for-Discord/blob/main/PS2%20games.md) from [Veritas83](https://github.com/Veritas83/PS2-OPL-CFG/blob/master/test/PS2-GAMEID-TITLE-MASTER.csv)
- [PS1 games.md](https://github.com/zorua98741/PS4-Rich-Presence-for-Discord/blob/main/PS1%20games.md) from [CRX](https://psxdatacenter.com/information.html)
- [Tustin](https://github.com/Tustin/PlayStationDiscord-Games/blob/master/script.py) – for the TMDB hashing approach

## Support the Original Author
Support zorua on Ko-fi: https://ko-fi.com/N4N87V7K5

## Disclaimer
```
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

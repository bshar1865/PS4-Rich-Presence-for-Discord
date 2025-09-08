using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using DiscordRPC;
using Newtonsoft.Json;
using FluentFTP;
using System.Windows.Input;

namespace PS4RichPresence
{
    public partial class MainWindow : BaseWindow
    {
        private DiscordRpcClient _discordClient;
        private Config _config;
        private System.Timers.Timer _updateTimer;
        private GameInfo _currentGame;
        private bool _ps4Connected;
        private readonly string _configPath;
        private readonly HttpClient _httpClient;
        private bool _isShuttingDown;
        private Timestamps _sessionTimestamp;
        private int _backoffStep;
        private string _lastTitleId;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                _httpClient = new HttpClient();
                
                // Set config path to Documents/PS4RPD-Config/
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var configDir = Path.Combine(documentsPath, "PS4RPD-Config");
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }
                _configPath = Path.Combine(configDir, "ps4rpd_config.json");
                
                LoadConfig();
                InitializeDiscord();
                InitializeTimer();
                _ = Task.Run(async () => await Dispatcher.InvokeAsync(InitializePS4Async));
                
                // Auto-connect if enabled
                _ = Task.Run(async () => await Dispatcher.InvokeAsync(AutoConnectIfEnabled));
                
                // Start minimized to tray if running on startup
                if (IsRunningOnStartup())
                {
                    Hide();
                }

                // Initialize session timestamp and last title
                _sessionTimestamp = null;
                _lastTitleId = null;
                _backoffStep = 0;

                // Handle window closing to minimize to tray
                Closing += (s, e) =>
                {
                    if (_isShuttingDown)
                    {
                        return;
                    }
                    
                    e.Cancel = true;
                    Hide();
                };

                // Handle application shutdown
                Application.Current.Exit += (s, e) =>
                {
                    _isShuttingDown = true;
                    _discordClient?.Dispose();
                    TrayIcon?.Dispose();
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during startup: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        protected override void Window_Close(object sender, RoutedEventArgs e)
        {
            if (_isShuttingDown)
            {
                Application.Current.Shutdown();
            }
            else
            {
                Hide();
                ShowTrayMessage("PS4 Rich Presence", "Application minimized to tray");
            }
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    _config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(_configPath));
                }
                else
                {
                    _config = new Config
                    {
                        IP = "",
                        ClientId = 1414584351571710113,
                        ShowPresenceOnHome = true,
                        MappedGames = new List<GameInfo>()
                    };
                    SaveConfig();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading config: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveConfig()
        {
            try
            {
                File.WriteAllText(_configPath, JsonConvert.SerializeObject(_config, Formatting.Indented));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving config: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitializeDiscord()
        {
            try
            {
                _discordClient = new DiscordRpcClient(_config.ClientId.ToString());
                _discordClient.Initialize();
                DiscordStatus.Text = "Discord: Connected";
            }
            catch (Exception ex)
            {
                DiscordStatus.Text = "Discord: Error";
                MessageBox.Show($"Error connecting to Discord: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitializeTimer()
        {
            // Fixed, lightweight watcher interval focused on change detection
            _updateTimer = new System.Timers.Timer(2000);
            _updateTimer.Elapsed += (s, e) => 
            {
                // Use Task.Run to ensure it's completely off the UI thread
                _ = Task.Run(async () => 
                {
                    try
                    {
                        await Dispatcher.InvokeAsync(UpdatePresence);
                    }
                    catch (Exception ex)
                    {
                        // Log errors but don't crash the app
                        System.Diagnostics.Debug.WriteLine($"Timer error: {ex.Message}");
                    }
                });
            };
            _updateTimer.Start();
        }

        private void SetTimerInterval(double milliseconds)
        {
            try
            {
                if (_updateTimer == null)
                {
                    return;
                }
                if (milliseconds < 500)
                {
                    milliseconds = 500;
                }
                _updateTimer.Interval = milliseconds;
            }
            catch { }
        }

        private async Task InitializePS4Async()
        {
            if (!string.IsNullOrEmpty(_config.IP))
            {
                await ConnectToPS4Async(_config.IP);
            }
            else
            {
                _ps4Connected = false;
                PS4Status.Text = "PS4: Disconnected";
            }

            // Update connect button text
            var connectButton = this.FindName("ConnectButton") as System.Windows.Controls.Button;
            if (connectButton != null)
            {
                connectButton.Content = _ps4Connected ? "Disconnect PS4" : "Connect PS4";
            }
        }

        private async void AutoConnectIfEnabled()
        {
            if (_config.AutoConnectOnStartup && !string.IsNullOrEmpty(_config.IP))
            {
                await ConnectToPS4Async(_config.IP);
                // Update connect button text
                var connectButton = this.FindName("ConnectButton") as System.Windows.Controls.Button;
                if (connectButton != null)
                {
                    connectButton.Content = _ps4Connected ? "Disconnect PS4" : "Connect PS4";
                }
            }
        }

        private bool IsRunningOnStartup()
        {
            try
            {
                // Check if app was started with startup arguments
                var args = Environment.GetCommandLineArgs();
                return args.Length > 1 && args[1] == "/startup";
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> ConnectToPS4Async(string ip)
        {
            try
            {
                // Show connecting message
                PS4Status.Text = "PS4: Connecting...";
                
                if (await TestPS4ConnectionAsync(ip))
                {
                    _ps4Connected = true;
                    _config.IP = ip;
                    SaveConfig();
                    PS4Status.Text = $"PS4: Connected to {ip}";
                    // Do not modify timestamp here; it is controlled by game change detection
                    _backoffStep = 0;
                    SetTimerInterval(2000);
                    return true;
                }
            }
            catch (Exception ex)
            {
                // Don't show error message for connection failures - just log them
                System.Diagnostics.Debug.WriteLine($"PS4 connection failed: {ex.Message}");
            }

            _ps4Connected = false;
            PS4Status.Text = "PS4: Disconnected";
            return false;
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            if (button != null)
            {
                if (_ps4Connected)
                {
                    // Disconnect
                    _ps4Connected = false;
                    SaveConfig();
                    PS4Status.Text = "PS4: Disconnected";
                    GameName.Text = "No game running";
                    GameImage.Source = null;
                    _discordClient?.ClearPresence();
                    // Do not reset timestamp here; only reset on title changes
                    button.Content = "Connect PS4";
                }
                else
                {
                    // Connect - allow connection even if PS4 is not available
                    button.IsEnabled = false;
                    button.Content = "Connecting...";
                    PS4Status.Text = "PS4: Connecting...";

                    try
                    {
                        // Only try the saved IP if available
                        if (!string.IsNullOrEmpty(_config.IP))
                        {
                            if (await ConnectToPS4Async(_config.IP))
                            {
                                button.Content = "Disconnect PS4";
                                // Trigger an immediate update without blocking
                                _ = Task.Run(async () => await Dispatcher.InvokeAsync(UpdatePresence));
                            }
                            else
                            {
                                // PS4 not available, but still show as "connected" for Discord
                                _ps4Connected = true;
                                button.Content = "Disconnect PS4";
                                PS4Status.Text = "PS4: Not Available";
                                
                                // Show "Not connected to PS4" on Discord immediately
                                if (_config.ShowPresenceWhenIdle)
                                {
                                    _discordClient.SetPresence(new RichPresence
                                    {
                                        State = "Not connected to PS4",
                                        Details = "Idle",
                                        Assets = new Assets
                                        {
                                            LargeImageKey = "idle",
                                            LargeImageText = "Idle"
                                        },
                                        Timestamps = null
                                    });
                                }
                                else
                                {
                                    _discordClient?.ClearPresence();
                                }
                            }
                        }
                        else
                        {
                            // If no saved IP, show settings dialog as non-modal
                            button.IsEnabled = true;
                            button.Content = "Connect PS4";
                            PS4Status.Text = "PS4: Disconnected";
                            
                            // Show settings dialog as a separate window
                            var settings = new SettingsDialog(_config);
                            settings.Owner = this;
                            settings.Show(); // Non-modal - doesn't block UI
                            
                            // Handle settings dialog closing
                            settings.Closed += async (s, args) =>
                            {
                                if (settings.DialogResult == true)
                                {
                                    _config = settings.Config;
                                    SaveConfig();
                                    await InitializePS4Async();
                                    
                                    // Update connect button text after initialization
                                    var connectButton = this.FindName("ConnectButton") as System.Windows.Controls.Button;
                                    if (connectButton != null)
                                    {
                                        connectButton.Content = _ps4Connected ? "Disconnect PS4" : "Connect PS4";
                                    }
                                }
                            };
                            return; // Exit early
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error connecting: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        button.Content = "Connect PS4";
                        PS4Status.Text = "PS4: Disconnected";
                    }

                    button.IsEnabled = true;
                }
            }
        }

        private async Task<bool> TestPS4ConnectionAsync(string ip)
        {
            try
            {
                using (var client = new FtpClient(ip))
                {
                    client.Port = 2121;
                    client.ConnectTimeout = 5000; // 5 second timeout for connection test
                    client.ReadTimeout = 5000;   // 5 second timeout for connection test
                    
                    // Use Task.Run with timeout to prevent blocking
                    var connectTask = Task.Run(() => client.Connect());
                    var timeoutTask = Task.Delay(5000);
                    
                    var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                    
                    if (completedTask == timeoutTask)
                    {
                        // Timeout occurred
                        return false;
                    }
                    
                    // Connection succeeded, now test directory
                    PS4Status.Text = "PS4: Verifying connection...";
                    
                    var directoryTask = Task.Run(() => client.DirectoryExists("/mnt/sandbox/NPXS20001_000"));
                    var directoryTimeoutTask = Task.Delay(3000); // 3 second timeout for directory check
                    
                    var directoryCompletedTask = await Task.WhenAny(directoryTask, directoryTimeoutTask);
                    
                    if (directoryCompletedTask == directoryTimeoutTask)
                    {
                        return false; // Directory check timed out
                    }
                    
                    return await directoryTask;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async void UpdatePresence()
        {
            // Allow presence updates even when not connected to PS4 (for idle status)
            if (!_ps4Connected && !_config.ShowPresenceWhenIdle) return;

            try
            {
                var gameInfo = await GetGameInfo();
                if (gameInfo != null)
                {
                    _currentGame = gameInfo;
                    GameName.Text = gameInfo.Name;
                    
                    if (!string.IsNullOrEmpty(gameInfo.ImageUrl))
                    {
                        try
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(gameInfo.ImageUrl);
                            bitmap.EndInit();
                            GameImage.Source = bitmap;
                        }
                        catch
                        {
                            GameImage.Source = null;
                        }
                    }

                    // Reset timestamp only when title changes
                    if (!string.Equals(_lastTitleId, gameInfo.TitleId, StringComparison.Ordinal))
                    {
                        _sessionTimestamp = Timestamps.Now;
                        _lastTitleId = gameInfo.TitleId;
                    }

                    _discordClient.SetPresence(new RichPresence
                    {
                        State = gameInfo.Name,
                        Assets = new Assets
                        {
                            LargeImageKey = gameInfo.ImageUrl
                        },
                        Timestamps = _sessionTimestamp
                    });

                    // Fast polling when a game is detected
                    _backoffStep = 0;
                    SetTimerInterval(2000);
                }
                else
                {
                    // PS4 not available, no game running, or no IP configured
                    if (string.IsNullOrEmpty(_config.IP))
                    {
                        GameName.Text = "No PS4 IP configured";
                }
                else
                {
                    GameName.Text = "PS4 Not Available";
                    }
                    GameImage.Source = null;
                    
                    // Show "Not connected to PS4" on Discord only if setting is enabled
                    if (_config.ShowPresenceWhenIdle)
                    {
                        var statusText = string.IsNullOrEmpty(_config.IP) ? "No PS4 configured" : "Not connected to PS4";
                    _discordClient.SetPresence(new RichPresence
                    {
                            State = statusText,
                        Details = "Idle",
                        Assets = new Assets
                        {
                            LargeImageKey = "idle",
                            LargeImageText = "Idle"
                        },
                            Timestamps = null
                        });
                    }
                    else
                    {
                        _discordClient?.ClearPresence();
                    }

                    // Moderate polling when idle/home or unknown
                    _backoffStep = 0;
                    SetTimerInterval(4000);
                }
            }
            catch (Exception ex)
            {
                // Handle PS4 disconnection gracefully
                if (ex.Message.Contains("Unable to connect") || ex.Message.Contains("Connection refused") || ex.Message.Contains("No connection"))
                {
                    // PS4 appears to be turned off or not accessible
                    _ps4Connected = false;
                    PS4Status.Text = "PS4: Disconnected";
                    GameName.Text = "PS4 Not Available";
                    GameImage.Source = null;
                    
                    // Update button text
                    var connectButton = this.FindName("ConnectButton") as System.Windows.Controls.Button;
                    if (connectButton != null)
                    {
                        connectButton.Content = "Connect PS4";
                    }
                    
                    // Show appropriate Discord status only if setting is enabled
                    if (_config.ShowPresenceWhenIdle)
                    {
                    _discordClient.SetPresence(new RichPresence
                    {
                        State = "PS4 appears to be turned off or is no longer on FTP",
                        Details = "Idle",
                        Assets = new Assets
                        {
                            LargeImageKey = "idle",
                            LargeImageText = "Idle"
                        },
                        Timestamps = null
                    });
                    }
                    else
                    {
                        _discordClient?.ClearPresence();
                    }
                    
                    // Keep timestamp; only reset on title change

                    // Exponential backoff up to 30s when offline
                    _backoffStep = Math.Min(_backoffStep + 1, 5);
                    var interval = 2000 * Math.Pow(2, _backoffStep); // 2s,4s,8s,16s,32s
                    if (interval > 30000)
                    {
                        interval = 30000;
                    }
                    SetTimerInterval(interval);
                }
                else
                {
                    // For non-connection errors, just log them without showing message boxes to avoid freezing
                    System.Diagnostics.Debug.WriteLine($"Error updating presence: {ex.Message}");
                }
            }
        }

        private async Task<GameInfo> GetGameInfo()
        {
            // If no IP is configured, return null to show idle status
            if (string.IsNullOrEmpty(_config.IP))
            {
                return null;
            }
            
            try
            {
                using (var client = new FtpClient(_config.IP))
                {
                    client.Port = 2121;
                    client.ConnectTimeout = 5000; // 5 second timeout
                    client.ReadTimeout = 5000;   // 5 second timeout
                    
                    // Use Task.Run with timeout to prevent blocking
                    var connectTask = Task.Run(() => client.Connect());
                    var timeoutTask = Task.Delay(5000);
                    
                    var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                    
                    if (completedTask == timeoutTask)
                    {
                        // Connection timed out
                        throw new InvalidOperationException("PS4 connection timed out");
                    }
                    
                    // Connection succeeded, now get file listing
                    var filesTask = Task.Run(() => client.GetListing("/mnt/sandbox"));
                    var filesTimeoutTask = Task.Delay(3000); // 3 second timeout for file listing
                    
                    var filesCompletedTask = await Task.WhenAny(filesTask, filesTimeoutTask);
                    
                    if (filesCompletedTask == filesTimeoutTask)
                    {
                        throw new InvalidOperationException("PS4 file listing timed out");
                    }
                    
                    var files = await filesTask;
                    
                    var titleId = files
                        .Where(item => !item.Name.StartsWith("NPXS"))
                        .Select(item => System.Text.RegularExpressions.Regex.Match(item.Name, @"[A-Z0-9]{4}[0-9]{5}"))
                        .FirstOrDefault(m => m.Success)?.Value;

                    if (string.IsNullOrEmpty(titleId))
                    {
                        if (_config.ShowPresenceOnHome)
                        {
                            return new GameInfo
                            {
                                TitleId = "main_menu",
                                Name = "PS4 Home Menu",
                                ImageUrl = "idle"
                            };
                        }
                        return null;
                    }

                    var mappedGame = _config.MappedGames.FirstOrDefault(g => g.TitleId == titleId);
                    if (mappedGame != null)
                    {
                        return mappedGame;
                    }

                    // Get game info from TMDB
                    var gameInfo = await GetGameInfoFromTMDB(titleId);
                    if (gameInfo != null)
                    {
                        _config.MappedGames.Add(gameInfo);
                        SaveConfig();
                        return gameInfo;
                    }

                    return new GameInfo
                    {
                        TitleId = titleId,
                        Name = titleId,
                        ImageUrl = titleId.ToLower()
                    };
                }
            }
            catch (Exception ex)
            {
                // Check if it's a connection-related error
                if (ex.Message.Contains("Unable to connect") || ex.Message.Contains("Connection refused") || 
                    ex.Message.Contains("No connection") || ex.Message.Contains("Timeout") ||
                    ex.Message.Contains("timed out") || ex.Message.Contains("connection failed") ||
                    ex.Message.Contains("connection timed out") || ex.Message.Contains("file listing timed out"))
                {
                    // PS4 is not accessible, throw a specific exception to be handled by UpdatePresence
                    throw new InvalidOperationException("PS4 appears to be turned off or is no longer on FTP");
                }
                
                // For other errors, just return null
                return null;
            }
        }

        private async Task<GameInfo> GetGameInfoFromTMDB(string titleId)
        {
            try
            {
                var titleIdExt = titleId + "_00";
                var tmdbKey = Convert.FromHexString("F5DE66D2680E255B2DF79E74F890EBF349262F618BCAE2A9ACCDEE5156CE8DF2CDF2D48C71173CDC2594465B87405D197CF1AED3B7E9671EEB56CA6753C2E6B0");
                
                using (var hmac = new System.Security.Cryptography.HMACSHA1(tmdbKey))
                {
                    var hash = BitConverter.ToString(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(titleIdExt))).Replace("-", "");
                    var url = $"http://tmdb.np.dl.playstation.net/tmdb2/{titleIdExt}_{hash}/{titleIdExt}.json";
                    
                    var json = await _httpClient.GetStringAsync(url);
                    var data = JsonConvert.DeserializeObject<dynamic>(json);
                    
                    return new GameInfo
                    {
                        TitleId = titleId,
                        Name = data.names[0].name,
                        ImageUrl = data.icons[0].icon
                    };
                }
            }
            catch
            {
                return null;
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SettingsDialog(_config);
            if (dialog.ShowDialog() == true)
            {
                _config = dialog.Config;
                SaveConfig();
                // Apply any UI-related settings and refresh presence
                    _ = Task.Run(async () => await Dispatcher.InvokeAsync(UpdatePresence));
            }
        }

        private void EditGame_Click(object sender, RoutedEventArgs e)
        {
            if (_currentGame == null)
            {
                MessageBox.Show("No game is currently running.", "Edit Game", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new EditGameDialog(_currentGame);
            if (dialog.ShowDialog() == true)
            {
                var index = _config.MappedGames.FindIndex(g => g.TitleId == _currentGame.TitleId);
                if (index != -1)
                {
                    _config.MappedGames[index] = dialog.GameInfo;
                }
                else
                {
                    _config.MappedGames.Add(dialog.GameInfo);
                }
                SaveConfig();
                UpdatePresence();
            }
        }

        private void ShowWindow_Click(object sender, RoutedEventArgs e)
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            _isShuttingDown = true;
            _discordClient?.Dispose();
            TrayIcon?.Dispose();
            Application.Current.Shutdown();
        }

        private void TrayIcon_Click(object sender, RoutedEventArgs e)
        {
            ShowWindow_Click(sender, e);
        }

        private void ShowTrayMessage(string title, string message)
        {
            TrayIcon.ShowBalloonTip(title, message, Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
        }

        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            var settings = new SettingsDialog(_config);
            if (settings.ShowDialog() == true)
            {
                _config = settings.Config;
                SaveConfig();
                await InitializePS4Async();
                
                // Update connect button text after initialization
                var connectButton = this.FindName("ConnectButton") as System.Windows.Controls.Button;
                if (connectButton != null)
                {
                    connectButton.Content = _ps4Connected ? "Disconnect PS4" : "Connect PS4";
                }
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isShuttingDown)
            {
                Application.Current.Shutdown();
            }
            else
            {
                Hide();
            }
        }
    }

    public class Config
    {
        public string IP { get; set; }
        public long ClientId { get; set; }
        public bool ShowPresenceOnHome { get; set; }
        public bool ShowPresenceWhenIdle { get; set; } = true;
        public bool AutoConnectOnStartup { get; set; } = false;
        public List<GameInfo> MappedGames { get; set; }
        public string Theme { get; set; } = "Light"; // Light or Dark
        public bool RunOnStartup { get; set; } = false;
    }

    public class GameInfo
    {
        public string TitleId { get; set; }
        public string Name { get; set; }
        public string ImageUrl { get; set; }
    }
} 
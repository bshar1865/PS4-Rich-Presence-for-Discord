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
        private readonly string _configPath = "ps4rpd_config.json";
        private readonly HttpClient _httpClient;
        private bool _isShuttingDown;
        private Timestamps _sessionTimestamp;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                _httpClient = new HttpClient();
                LoadConfig();
                InitializeDiscord();
                InitializeTimer();
                InitializePS4();

                // Initialize session timestamp
                _sessionTimestamp = null;

                // Handle window closing to minimize to tray
                Closing += (s, e) =>
                {
                    if (_isShuttingDown)
                    {
                        return;
                    }
                    
                    e.Cancel = true;
                    Hide();
                    ShowTrayMessage("PS4 Rich Presence", "Application minimized to tray");
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
            _updateTimer = new System.Timers.Timer(_config.UpdateInterval * 1000);
            _updateTimer.Elapsed += (s, e) => 
            {
                // Use fire-and-forget pattern to avoid blocking
                _ = Dispatcher.InvokeAsync(UpdatePresence);
            };
            _updateTimer.Start();
        }

        private void InitializePS4()
        {
            if (!string.IsNullOrEmpty(_config.IP))
            {
                ConnectToPS4(_config.IP);
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

        private bool ConnectToPS4(string ip)
        {
            try
            {
                if (TestPS4Connection(ip))
                {
                    _ps4Connected = true;
                    _config.IP = ip;
                    SaveConfig();
                    PS4Status.Text = $"PS4: Connected to {ip}";
                    // Initialize timestamp when connecting only if timer is enabled
                    if (_sessionTimestamp == null && _config.ShowTimer)
                    {
                        _sessionTimestamp = Timestamps.Now;
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to connect to PS4: {ex.Message}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            _ps4Connected = false;
            PS4Status.Text = "PS4: Disconnected";
            return false;
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
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
                    _sessionTimestamp = null;  // Reset timestamp on disconnect
                    button.Content = "Connect PS4";
                }
                else
                {
                    // Connect - allow connection even if PS4 is not available
                    button.IsEnabled = false;
                    button.Content = "Connecting...";

                    try
                    {
                        // Only try the saved IP if available
                        if (!string.IsNullOrEmpty(_config.IP))
                        {
                            if (ConnectToPS4(_config.IP))
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
                                // Show "Not connected to PS4" on Discord without blocking
                                _ = Task.Run(async () => await Dispatcher.InvokeAsync(UpdatePresence));
                            }
                        }
                        else
                        {
                            // If no saved IP, show settings dialog
                            Connect_Click(sender, e);
                            if (_ps4Connected)
                            {
                                button.Content = "Disconnect PS4";
                                // Trigger an immediate update without blocking
                                _ = Task.Run(async () => await Dispatcher.InvokeAsync(UpdatePresence));
                            }
                            else
                            {
                                button.Content = "Connect PS4";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error connecting: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        button.Content = "Connect PS4";
                    }

                    button.IsEnabled = true;
                }
            }
        }

        private bool TestPS4Connection(string ip)
        {
            try
            {
                using (var client = new FtpClient(ip))
                {
                    client.Port = 2121;
                    client.ConnectTimeout = 3000; // 3 second timeout for connection test
                    client.ReadTimeout = 3000;   // 3 second timeout for connection test
                    client.Connect();
                    
                    if (!client.DirectoryExists("/mnt/sandbox/NPXS20001_000"))
                    {
                        return false;
                    }
                    
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async void UpdatePresence()
        {
            if (!_ps4Connected) return;

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

                    // Initialize timestamp if not set and timer is enabled
                    if (_sessionTimestamp == null && _config.ShowTimer)
                    {
                        _sessionTimestamp = Timestamps.Now;
                    }

                    _discordClient.SetPresence(new RichPresence
                    {
                        State = gameInfo.Name,
                        Assets = new Assets
                        {
                            LargeImageKey = gameInfo.ImageUrl
                        },
                        Timestamps = _config.ShowTimer && _sessionTimestamp != null ? _sessionTimestamp : null
                    });
                }
                else
                {
                    // PS4 not available or no game running
                    GameName.Text = "PS4 Not Available";
                    GameImage.Source = null;
                    
                    // Show "Not connected to PS4" on Discord with app.ico
                    _discordClient.SetPresence(new RichPresence
                    {
                        State = "Not connected to PS4",
                        Details = "PS4 Rich Presence",
                        Assets = new Assets
                        {
                            LargeImageKey = "main_menu",
                            LargeImageText = "PS4 Rich Presence"
                        },
                        Timestamps = null // No timer when not connected
                    });
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
                    
                    // Show appropriate Discord status
                    _discordClient.SetPresence(new RichPresence
                    {
                        State = "PS4 appears to be turned off or is no longer on FTP",
                        Details = "PS4 Rich Presence",
                        Assets = new Assets
                        {
                            LargeImageKey = "main_menu",
                            LargeImageText = "PS4 Rich Presence"
                        },
                        Timestamps = null
                    });
                    
                    // Reset session timestamp
                    _sessionTimestamp = null;
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
            try
            {
                using (var client = new FtpClient(_config.IP))
                {
                    client.Port = 2121;
                    client.ConnectTimeout = 5000; // 5 second timeout
                    client.ReadTimeout = 5000;   // 5 second timeout
                    client.Connect();
                    var files = client.GetListing("/mnt/sandbox");
                    
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
                                ImageUrl = "main_menu"
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
                    ex.Message.Contains("timed out") || ex.Message.Contains("connection failed"))
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
                var oldShowTimer = _config.ShowTimer;
                _config = dialog.Config;
                SaveConfig();
                
                // Update timer interval efficiently
                if (_updateTimer != null)
                {
                    _updateTimer.Stop();
                _updateTimer.Interval = _config.UpdateInterval * 1000;
                    _updateTimer.Start();
                }
                
                // Reset timestamp if timer was disabled
                if (oldShowTimer && !_config.ShowTimer)
                {
                    _sessionTimestamp = null;
                    // Update presence immediately to remove timer
                    _ = Task.Run(async () => await Dispatcher.InvokeAsync(UpdatePresence));
                }
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

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            var settings = new SettingsDialog(_config);
            if (settings.ShowDialog() == true)
            {
                _config = settings.Config;
                SaveConfig();
                InitializePS4();
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
                ShowTrayMessage("PS4 Rich Presence", "Application minimized to tray");
            }
        }
    }

    public class Config
    {
        public string IP { get; set; }
        public long ClientId { get; set; }
        public int UpdateInterval { get; set; }
        public bool ShowPresenceOnHome { get; set; }
        public bool ShowTimer { get; set; }
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
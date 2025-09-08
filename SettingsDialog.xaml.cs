using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FluentFTP;
using System.Linq;
using System.Windows.Input;
using Microsoft.Win32;

namespace PS4RichPresence
{
    public partial class SettingsDialog : BaseWindow
    {
        public Config Config { get; private set; }

        public SettingsDialog(Config config)
        {
            InitializeComponent();
            Config = config;

            // Load current settings
            IPTextBox.Text = config.IP;
            ShowPresenceOnHomeCheckBox.IsChecked = config.ShowPresenceOnHome;
            ThemeComboBox.SelectedItem = ThemeComboBox.Items.Cast<ComboBoxItem>().FirstOrDefault(item => item.Content.ToString() == config.Theme);
            RunOnStartupCheckBox.IsChecked = config.RunOnStartup;

            // Check actual registry state
            using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"))
            {
                var registryValue = key?.GetValue("PS4 Rich Presence")?.ToString();
                if (registryValue != null)
                {
                    RunOnStartupCheckBox.IsChecked = true;
                    Config.RunOnStartup = true;
                }
            }
        }

        private async void AutoDetect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = (Button)sender;
                button.IsEnabled = false;
                button.Content = "Scanning...";

                var hostIP = GetLocalIPAddress();
                var subnet = hostIP.Substring(0, hostIP.LastIndexOf(".")) + ".";
                
                for (int i = 1; i < 255; i++)
                {
                    var ip = subnet + i;
                    if (await Task.Run(() => TestPS4Connection(ip)))
                    {
                        IPTextBox.Text = ip;
                        MessageBox.Show($"Found PS4 at {ip}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        break;
                    }
                }

                button.Content = "Auto Detect";
                button.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error scanning network: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetLocalIPAddress()
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                var endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint?.Address.ToString() ?? "127.0.0.1";
            }
        }

        private bool TestPS4Connection(string ip)
        {
            try
            {
                using (var client = new FtpClient(ip))
                {
                    client.Port = 2121;
                    client.Connect();
                    
                    if (!client.DirectoryExists("/mnt/sandbox/NPXS20001_000"))
                    {
                        return false;
                    }
                    
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Config.IP = IPTextBox.Text;
                Config.ShowPresenceOnHome = ShowPresenceOnHomeCheckBox.IsChecked ?? false;
                Config.Theme = ((ComboBoxItem)ThemeComboBox.SelectedItem).Content.ToString();
                Config.RunOnStartup = RunOnStartupCheckBox.IsChecked ?? false;

                // Handle startup registry
                using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (Config.RunOnStartup)
                    {
                        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                        key.SetValue("PS4 Rich Presence", exePath);
                    }
                    else
                    {
                        key.DeleteValue("PS4 Rich Presence", false);
                    }
                }

                // Apply theme immediately
                var app = (App)Application.Current;
                if (Config.Theme == "Dark")
                {
                    var darkTheme = app.Resources["DarkTheme"] as ResourceDictionary;
                    if (darkTheme != null)
                    {
                        app.Resources.MergedDictionaries[0] = darkTheme;
                    }
                }
                else
                {
                    var lightTheme = new ResourceDictionary
                    {
                        Source = new Uri("pack://application:,,,/PS4RichPresence;component/Themes/LightTheme.xaml", UriKind.Absolute)
                    };
                    app.Resources.MergedDictionaries[0] = lightTheme;
                }

                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var ip = IPTextBox.Text.Trim();
                if (string.IsNullOrEmpty(ip))
                {
                    MessageBox.Show("Please enter an IP address.", "Connection", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var button = sender as System.Windows.Controls.Button;
                if (button != null)
                {
                    button.IsEnabled = false;
                    button.Content = "Connecting...";
                }

                if (TestPS4Connection(ip))
                {
                    MessageBox.Show($"Successfully connected to PS4 at {ip}", "Connection", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Could not connect to PS4 at {ip}", "Connection", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                if (button != null)
                {
                    button.IsEnabled = true;
                    button.Content = "Connect";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error connecting to PS4: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            DialogResult = false;
            Close();
        }
    }
} 
using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FluentFTP;

namespace PS4RichPresence
{
    public partial class SettingsDialog : Window
    {
        public Config Config { get; private set; }

        public SettingsDialog(Config config)
        {
            InitializeComponent();
            Config = config;

            // Load current settings
            IPTextBox.Text = config.IP;
            IntervalTextBox.Text = config.UpdateInterval.ToString();
            HibernateTimeTextBox.Text = config.HibernateTime.ToString();
            RetroCoversCheckBox.IsChecked = config.RetroCovers;
            HibernateCheckBox.IsChecked = config.Hibernate;
            ShowPresenceOnHomeCheckBox.IsChecked = config.ShowPresenceOnHome;
            ShowTimerCheckBox.IsChecked = config.ShowTimer;
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
                Config.UpdateInterval = int.Parse(IntervalTextBox.Text);
                Config.HibernateTime = int.Parse(HibernateTimeTextBox.Text);
                Config.RetroCovers = RetroCoversCheckBox.IsChecked ?? false;
                Config.Hibernate = HibernateCheckBox.IsChecked ?? false;
                Config.ShowPresenceOnHome = ShowPresenceOnHomeCheckBox.IsChecked ?? false;
                Config.ShowTimer = ShowTimerCheckBox.IsChecked ?? false;

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
    }
} 
using System;
using System.IO;
using System.IO.Pipes;
using System.Windows;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace PS4RichPresence
{
    public partial class App : Application
    {
        private const string PipeName = "PS4RichPresenceApp";
        private NamedPipeServerStream _pipeServer;
        private CancellationTokenSource _cancellationTokenSource;
        private MainWindow _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Set up config directory at the beginning
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var configDir = Path.Combine(documentsPath, "PS4RPD-Config");
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }
            
            try
            {
                // Try to create the pipe server
                try
                {
                    _pipeServer = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Message);
                    _cancellationTokenSource = new CancellationTokenSource();
                    ListenForOtherInstances();
                }
                catch (IOException)
                {
                    // Another instance exists, send message and exit
                    using (var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                    {
                        try
                        {
                            pipeClient.Connect(1000); // Wait up to 1 second
                            Environment.Exit(0);
                            return;
                        }
                        catch (Exception)
                        {
                            // If we can't connect, assume no other instance and continue
                        }
                    }
                }

                var startupLogPath = Path.Combine(configDir, "startup.log");
                File.WriteAllText(startupLogPath, "Application starting...\n");
                
                AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                {
                    var crashLogPath = Path.Combine(configDir, "crash.log");
                    File.WriteAllText(crashLogPath, args.ExceptionObject.ToString());
                    MessageBox.Show($"An error occurred: {args.ExceptionObject}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                };

                this.DispatcherUnhandledException += (sender, args) =>
                {
                    var crashLogPath = Path.Combine(configDir, "crash.log");
                    File.WriteAllText(crashLogPath, args.Exception.ToString());
                    MessageBox.Show($"An error occurred: {args.Exception}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    args.Handled = true;
                };

                _mainWindow = new MainWindow();
                MainWindow = _mainWindow;
                
                // Check if this is a startup launch
                bool isStartupLaunch = e.Args.Length > 0 && e.Args[0] == "/startup";
                
                if (isStartupLaunch)
                {
                    // Don't show window on startup, it will be minimized to tray
                    _mainWindow.WindowState = WindowState.Minimized;
                }
                else
                {
                    _mainWindow.Show();
                }

                base.OnStartup(e);

                // Load theme from config if it exists
                var configPath = Path.Combine(configDir, "ps4rpd_config.json");
                if (File.Exists(configPath))
                {
                    try
                    {
                        var config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(configPath));
                        if (config != null && !string.IsNullOrEmpty(config.Theme))
                        {
                            if (config.Theme == "Dark")
                            {
                                var darkTheme = Resources["DarkTheme"] as ResourceDictionary;
                                if (darkTheme != null)
                                {
                                    Resources.MergedDictionaries[0] = darkTheme;
                                }
                            }
                            else
                            {
                                var lightTheme = new ResourceDictionary
                                {
                                    Source = new Uri("pack://application:,,,/PS4RichPresence;component/Themes/LightTheme.xaml", UriKind.Absolute)
                                };
                                Resources.MergedDictionaries[0] = lightTheme;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // If there's an error loading the config, we'll just use the default light theme
                    }
                }
            }
            catch (Exception ex)
            {
                // Use the same configDir that was already created above
                var errorLogPath = Path.Combine(configDir, "error.log");
                File.WriteAllText(errorLogPath, $"Startup error: {ex}\n{ex.StackTrace}");
                MessageBox.Show($"Error during startup: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Current.Shutdown();
            }
        }

        private void ListenForOtherInstances()
        {
            Task.Run(async () =>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        await _pipeServer.WaitForConnectionAsync(_cancellationTokenSource.Token);
                        await Dispatcher.InvokeAsync(() =>
                        {
                            if (_mainWindow != null)
                            {
                                _mainWindow.Show();
                                _mainWindow.WindowState = WindowState.Normal;
                                _mainWindow.Activate();
                                _mainWindow.Focus();
                            }
                        });
                        _pipeServer.Disconnect();
                        _pipeServer.WaitForConnection();
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception)
                    {
                        // Log or handle other exceptions if needed
                    }
                }
            });
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            _pipeServer?.Dispose();
            base.OnExit(e);
        }
    }
} 
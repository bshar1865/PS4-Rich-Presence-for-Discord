using System;
using System.IO;
using System.IO.Pipes;
using System.Windows;
using System.Threading;
using System.Threading.Tasks;

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

                File.WriteAllText("startup.log", "Application starting...\n");
                
                AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                {
                    File.WriteAllText("crash.log", args.ExceptionObject.ToString());
                    MessageBox.Show($"An error occurred: {args.ExceptionObject}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                };

                this.DispatcherUnhandledException += (sender, args) =>
                {
                    File.WriteAllText("crash.log", args.Exception.ToString());
                    MessageBox.Show($"An error occurred: {args.Exception}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    args.Handled = true;
                };

                _mainWindow = new MainWindow();
                MainWindow = _mainWindow;
                _mainWindow.Show();

                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                File.WriteAllText("error.log", $"Startup error: {ex}\n{ex.StackTrace}");
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
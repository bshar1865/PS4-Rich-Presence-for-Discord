using System.Windows;
using System.Windows.Input;

namespace PS4RichPresence
{
    public class BaseWindow : Window
    {
        public BaseWindow()
        {
            Style = (Style)Application.Current.Resources["CustomWindowStyle"];
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
        }

        protected void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        protected void Window_Minimize(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        protected void Window_Maximize(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        protected virtual void Window_Close(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
} 
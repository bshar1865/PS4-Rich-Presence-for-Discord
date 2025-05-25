using System.Windows;

namespace PS4RichPresence
{
    public partial class InputDialog : Window
    {
        public string ResponseText
        {
            get { return ResponseTextBox.Text; }
            set { ResponseTextBox.Text = value; }
        }

        public InputDialog(string title, string message, string defaultValue = "")
        {
            InitializeComponent();
            Title = title;
            Message.Text = message;
            ResponseText = defaultValue;
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
} 
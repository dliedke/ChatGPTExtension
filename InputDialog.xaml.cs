using System;
using System.Windows;
using System.Windows.Input;

namespace ChatGPTExtension
{
    public partial class InputDialog : Window
    {
        public InputDialog(string title, string question, string defaultAnswer = "")
        {
            InitializeComponent();
            this.Title = title;
            lblQuestion.Content = question;
            txtAnswer.Text = defaultAnswer;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            // Attach KeyDown event handler to the TextBox
            txtAnswer.KeyDown += TxtAnswer_KeyDown;

            // Select txtAnswer until the "." in the string
            int index = defaultAnswer.IndexOf('.');
            if (index > 0)
            {
                txtAnswer.Select(0, index);
            }
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            txtAnswer.Focus(); // Set focus to the TextBox when the window is activated
        }

        private void TxtAnswer_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                btnDialogOk_Click(this, new RoutedEventArgs());
            }
            else if (e.Key == Key.Escape)
            {
                btnDialogCancel_Click(this, new RoutedEventArgs());
            }
        }

        private void btnDialogOk_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void btnDialogCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        public string ResponseText
        {
            get { return txtAnswer.Text; }
        }
    }
}
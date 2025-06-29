using System.Windows;

namespace ChatGPTExtension
{
    public partial class ButtonNamesWindow : Window
    {
        public ButtonNames EditedNames { get; private set; }

        public ButtonNamesWindow(ButtonNames current)
        {
            InitializeComponent();
            EditedNames = new ButtonNames
            {
                VSNETToGPT = current.VSNETToGPT,
                FixCodeInGPT = current.FixCodeInGPT,
                ImproveCodeInGPT = current.ImproveCodeInGPT,
                GPTToVSNET = current.GPTToVSNET,
                ContinueCode = current.ContinueCode,
                CompleteCode = current.CompleteCode,
                NewFile = current.NewFile,
                AttachFile = current.AttachFile
            };
            DataContext = EditedNames;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

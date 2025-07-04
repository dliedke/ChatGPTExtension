using System.Windows;

namespace ChatGPTExtension
{
    public partial class ButtonsConfigWindow : Window
    {
        private readonly ButtonLabelsConfiguration _config;
        public ButtonsConfigWindow(ButtonLabelsConfiguration config)
        {
            InitializeComponent();
            _config = config;
            LoadValues();
        }

        private void LoadValues()
        {
            VSNETToAITxt.Text = _config.VSNETToAI;

            FixCodeTxt.Text = _config.FixCode;
            FixCodePromtTxt.Text = _config.FixCodePromt;

            ImproveCodeTxt.Text = _config.ImproveCode;
            ImproveCodePromtTxt.Text = _config.ImproveCodePromt;

            CompleteCodeTxt.Text = _config.CompleteCode;
            CompleteCodePromtTxt.Text = _config.CompleteCodePromt;

            ContinueCodeTxt.Text = _config.ContinueCode;
            ContinueCodePromtTxt.Text = _config.ContinueCodePromt;

            AIToVSNETTxt.Text = _config.AIToVSNET;

            NewFileTxt.Text = _config.NewFile;
            AttachFileTxt.Text = _config.AttachFile;
            EnableCopyCodeTxt.Text = _config.EnableCopyCode;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _config.VSNETToAI = VSNETToAITxt.Text;

            _config.FixCode = FixCodeTxt.Text;
            _config.FixCodePromt = FixCodePromtTxt.Text;

            _config.ImproveCode = ImproveCodeTxt.Text;
            _config.ImproveCodePromt = ImproveCodePromtTxt.Text;

            _config.CompleteCode = CompleteCodeTxt.Text;
            _config.CompleteCodePromt = CompleteCodePromtTxt.Text;

            _config.ContinueCode = ContinueCodeTxt.Text;
            _config.ContinueCodePromt = ContinueCodePromtTxt.Text;

            _config.AIToVSNET = AIToVSNETTxt.Text;
            _config.NewFile = NewFileTxt.Text;
            _config.AttachFile = AttachFileTxt.Text;
            _config.EnableCopyCode = EnableCopyCodeTxt.Text;
            _config.Save();
            DialogResult = true;
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            // Neue Instanz mit Standardwerten erstellen
            ButtonLabelsConfiguration defaultValues = new ButtonLabelsConfiguration();

            // Die Textfelder mit den Standardwerten aktualisieren
            VSNETToAITxt.Text = defaultValues.VSNETToAI;

            FixCodeTxt.Text = defaultValues.FixCode;
            FixCodePromtTxt.Text = defaultValues.FixCodePromt;

            ImproveCodeTxt.Text = defaultValues.ImproveCode;
            ImproveCodePromtTxt.Text = defaultValues.ImproveCodePromt;

            CompleteCodeTxt.Text = defaultValues.CompleteCode;
            CompleteCodePromtTxt.Text = defaultValues.CompleteCodePromt;

            ContinueCodeTxt.Text = defaultValues.ContinueCode;
            ContinueCodePromtTxt.Text = defaultValues.ContinueCodePromt;

            AIToVSNETTxt.Text = defaultValues.AIToVSNET;
            NewFileTxt.Text = defaultValues.NewFile;
            AttachFileTxt.Text = defaultValues.AttachFile;
            EnableCopyCodeTxt.Text = defaultValues.EnableCopyCode;

            // Optional: Wenn du die Reset-Werte auch speichern möchtest, kannst du das hier tun
            // _config.VSNETToAI = defaultValues.VSNETToAI;
            // ... und so weiter für alle Properties
            // _config.Save();

            // Wenn das Fenster geschlossen werden soll, nachdem die Werte zurückgesetzt wurden
            // DialogResult = true;             
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}

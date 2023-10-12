using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Collections.Generic;

using Newtonsoft.Json;

namespace ChatGPTExtension
{
    public partial class ConfigurationWindow : Window
    {
        #region ConfigModel class

        public class ConfigModel
        {
            public Dictionary<string, string> Actions { get; set; }
        }

        #endregion

        #region Initialization / Constructor

        private const string _configurationFileName = "actions.json";

        private static readonly string _appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChatGPTExtension", "Actions");

        private static readonly string _fullConfigPath = Path.Combine(_appDataPath, _configurationFileName);

        private bool _dataChanged;

        public Dictionary<string, string> ConfigurationList { get; set; }

        public ConfigurationWindow()
        {
            try
            {
                InitializeComponent();
                LoadConfiguration();

                _dataChanged = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred in ConfigurationWindow: {ex.Message}");
            }
        }

        #endregion

        #region Load Configuration

        private void LoadConfiguration()
        {
            // Load configuration from json file
            ConfigurationList = LoadConfigurationFromFile();

            // Show the configuration on the screen
            if (ConfigurationList != null)
            {
                Action1NameTextBox.Text = ConfigurationList.Count > 0 ? ConfigurationList.Keys.ElementAt(0) : string.Empty;
                Action1PromptTextBox.Text = ConfigurationList.Count > 0 ? ConfigurationList.Values.ElementAt(0) : string.Empty;

                Action2NameTextBox.Text = ConfigurationList.Count > 1 ? ConfigurationList.Keys.ElementAt(1) : string.Empty;
                Action2PromptTextBox.Text = ConfigurationList.Count > 1 ? ConfigurationList.Values.ElementAt(1) : string.Empty;

                Action3NameTextBox.Text = ConfigurationList.Count > 2 ? ConfigurationList.Keys.ElementAt(2) : string.Empty;
                Action3PromptTextBox.Text = ConfigurationList.Count > 2 ? ConfigurationList.Values.ElementAt(2) : string.Empty;

                Action4NameTextBox.Text = ConfigurationList.Count > 3 ? ConfigurationList.Keys.ElementAt(3) : string.Empty;
                Action4PromptTextBox.Text = ConfigurationList.Count > 3 ? ConfigurationList.Values.ElementAt(3) : string.Empty;

                Action5NameTextBox.Text = ConfigurationList.Count > 4 ? ConfigurationList.Keys.ElementAt(4) : string.Empty;
                Action5PromptTextBox.Text = ConfigurationList.Count > 4 ? ConfigurationList.Values.ElementAt(4) : string.Empty;

                Action6NameTextBox.Text = ConfigurationList.Count > 5 ? ConfigurationList.Keys.ElementAt(5) : string.Empty;
                Action6PromptTextBox.Text = ConfigurationList.Count > 5 ? ConfigurationList.Values.ElementAt(5) : string.Empty;

                Action7NameTextBox.Text = ConfigurationList.Count > 6 ? ConfigurationList.Keys.ElementAt(6) : string.Empty;
                Action7PromptTextBox.Text = ConfigurationList.Count > 6 ? ConfigurationList.Values.ElementAt(6) : string.Empty;

                Action8NameTextBox.Text = ConfigurationList.Count > 7 ? ConfigurationList.Keys.ElementAt(7) : string.Empty;
                Action8PromptTextBox.Text = ConfigurationList.Count > 7 ? ConfigurationList.Values.ElementAt(7) : string.Empty;

                Action9NameTextBox.Text = ConfigurationList.Count > 8 ? ConfigurationList.Keys.ElementAt(8) : string.Empty;
                Action9PromptTextBox.Text = ConfigurationList.Count > 8 ? ConfigurationList.Values.ElementAt(8) : string.Empty;

                Action10NameTextBox.Text = ConfigurationList.Count > 9 ? ConfigurationList.Keys.ElementAt(9) : string.Empty;
                Action10PromptTextBox.Text = ConfigurationList.Count > 9 ? ConfigurationList.Values.ElementAt(9) : string.Empty;
            }
        }

        public Dictionary<string, string> LoadConfigurationFromFile()
        {
            // If we don't have a json file, retreive default actions 
            // and save a new file
            if (!File.Exists(_fullConfigPath))
            {
                var defaultActions = GetDefaultActions();
                SaveConfigurationToFile(defaultActions);
                return defaultActions;
            }

            // Load json file with configurations
            var json = File.ReadAllText(_fullConfigPath);
            var config = JsonConvert.DeserializeObject<ConfigModel>(json);

            return config.Actions;
        }

        private Dictionary<string, string> GetDefaultActions()
        {
            // Return default initial actions for this extension
            return new Dictionary<string, string> {
                                        { "Error Handling", "Please add error handling to the following {languageCode} code:" },
                                        { "Optimize","Please optimize the following {languageCode} code for performance:" },
                                        { "Comment","Please comment the following {languageCode} code:" },
                                        { "Unit Tests","Please create unit tests for the following {languageCode} code:" },
                                        { "Security Issues","Please identify and fix security issues in the following {languageCode} code:" },
                                        { "Thread-safe","Please make the following {languageCode} code thread-safe:" },
                                        { "Explain","Please help me understand the following {languageCode} code:" }
                                    };
        }

        #endregion

        #region Save Configuration

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            SaveConfiguration();
        }

        private bool SaveConfiguration()
        {
            try
            {
                // Check duplicate action names
                var textBoxValues = new List<string>
                                        {
                                            Action1NameTextBox.Text?.Trim(),
                                            Action2NameTextBox.Text?.Trim(),
                                            Action3NameTextBox.Text?.Trim(),
                                            Action4NameTextBox.Text?.Trim(),
                                            Action5NameTextBox.Text?.Trim(),
                                            Action6NameTextBox.Text?.Trim(),
                                            Action7NameTextBox.Text?.Trim(),
                                            Action8NameTextBox.Text?.Trim(),
                                            Action9NameTextBox.Text?.Trim(),
                                            Action10NameTextBox.Text?.Trim()
                                        };

                if (HasDuplicates(textBoxValues))
                {
                    MessageBox.Show("Duplicate action names detected. Please ensure each action has a unique name.", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                var config = new Dictionary<string, string>();

                // Check if we have actions without prompt or vice-versa
                if (!AddToConfigIfValid(Action1NameTextBox.Text?.Trim(), Action1PromptTextBox.Text?.Trim(), config) ||
                    !AddToConfigIfValid(Action2NameTextBox.Text?.Trim(), Action2PromptTextBox.Text?.Trim(), config) ||
                    !AddToConfigIfValid(Action3NameTextBox.Text?.Trim(), Action3PromptTextBox.Text?.Trim(), config) ||
                    !AddToConfigIfValid(Action4NameTextBox.Text?.Trim(), Action4PromptTextBox.Text?.Trim(), config) ||
                    !AddToConfigIfValid(Action5NameTextBox.Text?.Trim(), Action5PromptTextBox.Text?.Trim(), config) ||
                    !AddToConfigIfValid(Action6NameTextBox.Text?.Trim(), Action6PromptTextBox.Text?.Trim(), config) ||
                    !AddToConfigIfValid(Action7NameTextBox.Text?.Trim(), Action7PromptTextBox.Text?.Trim(), config) ||
                    !AddToConfigIfValid(Action8NameTextBox.Text?.Trim(), Action8PromptTextBox.Text?.Trim(), config) ||
                    !AddToConfigIfValid(Action9NameTextBox.Text?.Trim(), Action9PromptTextBox.Text?.Trim(), config) ||
                    !AddToConfigIfValid(Action10NameTextBox.Text?.Trim(), Action10PromptTextBox.Text?.Trim(), config))
                {
                    MessageBox.Show("Please ensure that each action name provided has a prompt assigned.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                // Save the current configuration to json file
                SaveConfigurationToFile(config);
                _dataChanged = false;
                DialogResult = true;

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while saving the configuration: {ex.Message}");
                MessageBox.Show("An error occurred while saving the configuration. Please check the details and try again.", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool AddToConfigIfValid(string key, string value, Dictionary<string, string> config)
        {
            // Correct data
            if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
            {
                config[key] = value;
                return true;
            }
            // Empty data
            else if (string.IsNullOrEmpty(key) && string.IsNullOrEmpty(value))
            {
                return true;
            }
            // Bad data
            else
            {
                return false;
            }
        }

        private bool HasDuplicates(List<string> items)
        {
            return items.Where(x => !string.IsNullOrEmpty(x)).GroupBy(x => x).Any(group => group.Count() > 1);
        }

        private void SaveConfigurationToFile(Dictionary<string, string> actions)
        {
            // Create new directory is required
            if (!Directory.Exists(_appDataPath))
            {
                Directory.CreateDirectory(_appDataPath);
            }

            // Write json file with configuration
            var config = new ConfigModel
            {
                Actions = actions
            };

            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(_fullConfigPath, json);
        }

        #endregion

        #region Closing / Data changed

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            // Just close
            this.Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // If we have changed the config, ask if user wants to save it
            if (_dataChanged)
            {
                MessageBoxResult result = MessageBox.Show("Do you want to save changes?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);

                switch (result)
                {
                    case MessageBoxResult.Yes:
                        bool success = SaveConfiguration();

                        // If save failed, do not close the window
                        if (!success)
                            e.Cancel = true;
                        break;
                    case MessageBoxResult.No:
                        break;
                }
            }
        }

        private void ActionTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // Configuration was updated
            _dataChanged = true;
        }

        #endregion
    }
}

/* *******************************************************************************************************************
 * Application: ChatGPTExtension
 * 
 * Autor:  Daniel Liedke
 * 
 * Copyright © Daniel Liedke 2025
 * Usage and reproduction in any manner whatsoever without the written permission of Daniel Liedke is strictly forbidden.
 *  
 * Purpose: User control to configure custom actions for GPT
 *           
 * *******************************************************************************************************************/

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ChatGPTExtension
{
    public partial class ConfigurationWindow : Window
    {
        #region Class Variables

        private const string _configurationFileName = "actions.json";
        private static readonly string _appDataPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChatGPTExtension", "Actions");
        private static readonly string _fullConfigPath = System.IO.Path.Combine(_appDataPath, _configurationFileName);
        public static bool _dataChanged = false;

        #endregion

        #region Properties

        /// <summary>
        /// List of actions to be displayed and manipulated on the UI.
        /// </summary>
        public ObservableCollection<ActionItem> ActionItems { get; set; } = new ObservableCollection<ActionItem>();

        #endregion

        #region Constructor

        public ConfigurationWindow()
        {
            InitializeComponent();
            DataContext = this;
            LoadConfiguration();

            new ListViewDragDropManager<ActionItem>(this.ActionListView);
        }

        #endregion

        #region Load/Save configuration

        /// <summary>
        /// Load existing configurations or default ones if none exists.
        /// </summary>
        private void LoadConfiguration()
        {
            var configurations = LoadConfigurationFromFile();
            foreach (var config in configurations)
            {
                AddActionItem(config);
            }
        }

        /// <summary>
        /// Load configurations from a JSON file. If the file doesn't exist, create one with default actions.
        /// </summary>
        private List<ActionItem> LoadConfigurationFromFile()
        {
            try
            {
                if (!File.Exists(_fullConfigPath))
                {
                    var defaultActions = GetDefaultActions();
                    SaveConfigurationToFile(defaultActions);
                    _dataChanged = true;
                    return defaultActions;
                }

                var json = File.ReadAllText(_fullConfigPath);
                var configuration = JsonConvert.DeserializeObject<List<ActionItem>>(json);

                _dataChanged = false;
                return configuration;
            }
            catch
            {
                // Old configurations will not work, load default actions
                var defaultActions = GetDefaultActions();
                SaveConfigurationToFile(defaultActions);
                return defaultActions;
            }
        }

        /// <summary>
        /// Save the current configurations to the JSON file.
        /// </summary>
        private void SaveConfiguration()
        {
            SaveConfigurationToFile(ActionItems.ToList());
            _dataChanged = false;
        }

        /// <summary>
        /// Write configurations to a JSON file.
        /// </summary>
        private void SaveConfigurationToFile(List<ActionItem> actions)
        {
            if (!Directory.Exists(_appDataPath))
            {
                Directory.CreateDirectory(_appDataPath);
            }

            var json = JsonConvert.SerializeObject(actions, Formatting.Indented);
            File.WriteAllText(_fullConfigPath, json);
        }

        private List<ActionItem> GetDefaultActions()
        {
            // Return default initial actions for this extension
            return new List<ActionItem> {
                                  new ActionItem { Name = "Error Handling", Prompt = "Please add error handling to the following {languageCode} code:" },
                                  new ActionItem { Name = "Optimize", Prompt = "Please optimize the following {languageCode} code for performance:" },
                                  new ActionItem { Name = "Comment", Prompt = "Please add comments to the following {languageCode} code:" },
                                  new ActionItem { Name = "Unit Tests", Prompt = "Please create unit tests for the following {languageCode} code:" },
                                  new ActionItem { Name = "Security Issues", Prompt = "Please identify and fix security issues in the following {languageCode} code:" },
                                  new ActionItem { Name = "Thread-safe", Prompt = "Please make the following {languageCode} code thread-safe:" },
                                  new ActionItem { Name = "Explain", Prompt = "Please help me understand the following {languageCode} code:" }
                              };
        }

        #endregion

        #region Save Click / Cancel Click

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            SaveConfiguration();
            DialogResult = true;
            Close();
        }

        private void OnResetClick(object sender, RoutedEventArgs e)
        {
            // 1. Die Standardaktionen abrufen
            List<ActionItem> defaultActions = GetDefaultActions();

            // 2. Die aktuelle ActionItems-Collection leeren
            ActionItems.Clear();

            // 3. Die Standardaktionen der Collection hinzufügen
            foreach (var action in defaultActions)
            {
                AddActionItem(action); // Nutzt deine bestehende Methode zum Hinzufügen
            }

            // 4. Den _dataChanged-Flag setzen, da sich die Daten geändert haben
            _dataChanged = true;

            // Optional: Wenn du das Fenster nach dem Reset schließen möchtest, aktiviere die nächste Zeile
            // DialogResult = true;
            // Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

        #region Move Up/Down/Delete/Add

        private void MoveUpButton_Click(object sender, RoutedEventArgs e)
        {
            // Find the ListViewItem that contains the clicked button
            var button = (Button)sender;
            ListViewItem item = FindListViewItem(button);

            if (item != null)
            {
                var actionToMove = (ActionItem)item.DataContext;
                int index = ActionItems.IndexOf(actionToMove);
                if (index > 0)
                {
                    ActionItems.Move(index, index - 1);
                }
            }

            _dataChanged = true;
        }

        private void MoveDownButton_Click(object sender, RoutedEventArgs e)
        {
            // Find the ListViewItem that contains the clicked button
            var button = (Button)sender;
            ListViewItem item = FindListViewItem(button);

            if (item != null)
            {
                var actionToMove = (ActionItem)item.DataContext;
                int index = ActionItems.IndexOf(actionToMove);
                if (index < ActionItems.Count - 1)
                {
                    ActionItems.Move(index, index + 1);
                }
            }

            _dataChanged = true;
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            // Find the ListViewItem that contains the clicked button
            var button = (Button)sender;
            ListViewItem item = FindListViewItem(button);

            if (item != null)
            {
                var actionToDelete = (ActionItem)item.DataContext;
                RemoveActionItem(actionToDelete);
            }
        }

        private ListViewItem FindListViewItem(DependencyObject child)
        {
            while (child != null && !(child is ListViewItem))
            {
                child = VisualTreeHelper.GetParent(child);
            }
            return child as ListViewItem;
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            // Max 20
            if (ActionItems.Count >= 20)
            {
                MessageBox.Show("Maxium of 20 custom actions can be added!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Create a new empty ActionItem
            var newAction = new ActionItem { Name = "New Action", Prompt = "Enter prompt here" };

            // Add it to the ObservableCollection
            ActionItems.Add(newAction);

            // Set the new item as the selected one in the ListView
            ActionListView.SelectedItem = newAction;

            // Scroll the ListView to the newly added item
            ActionListView.ScrollIntoView(newAction);

            _dataChanged = true;
        }

        private void AddActionItem(ActionItem item)
        {
            // Add the item to the ObservableCollection
            item.PropertyChanged += ActionItem_PropertyChanged;
            ActionItems.Add(item);
        }

        private void RemoveActionItem(ActionItem item)
        {
            // Remove the item from the ObservableCollection
            item.PropertyChanged -= ActionItem_PropertyChanged;
            ActionItems.Remove(item);

            _dataChanged = true;
        }

        #endregion

        #region Data Change detection / Closing Window

        private void ActionItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Configuration was updated
            _dataChanged = true;
        }

        private void ConfigurationWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // If we have changed the config, ask if user wants to save it
            if (_dataChanged)
            {
                MessageBoxResult result = MessageBox.Show("Do you want to save changes?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);

                switch (result)
                {
                    case MessageBoxResult.Yes:
                        SaveConfiguration();
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

        #region GPT Wide Button

        private void ConfigureGPTWideButton_Click(object sender, RoutedEventArgs e)
        {
            // Show GPT wide script configuration screen
            var gptWideWindow = new GPTWideWindow
            {
                Owner = this
            };
            gptWideWindow.ShowDialog();
        }

        #endregion

        #region Proxy Configuration Button

        private void ProxyConfigButton_Click(object sender, RoutedEventArgs e)
        {
            // Show proxy configuration screen
            var proxyConfigWindow = new ProxyConfigurationWindow
            {
                Owner = this
            };
            proxyConfigWindow.ShowDialog();
        }

        #endregion
    }
}

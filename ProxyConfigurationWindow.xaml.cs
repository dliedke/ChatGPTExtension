/* *******************************************************************************************************************
 * Application: ChatGPTExtension
 * 
 * Autor:  Daniel Liedke
 * 
 * Copyright © Daniel Liedke 2025
 * Usage and reproduction in any manner whatsoever without the written permission of Daniel Liedke is strictly forbidden.
 *  
 * Purpose: Proxy Configuration Window for ChatGPT Extension
 *           
 * *******************************************************************************************************************/

using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.VisualStudio.Shell;

namespace ChatGPTExtension
{
    public partial class ProxyConfigurationWindow : Window
    {
        private Configuration _configuration;

        public ProxyConfigurationWindow()
        {
            InitializeComponent();
            LoadConfiguration();
            UpdateUIState();
        }

        private void LoadConfiguration()
        {
            try
            {
                _configuration = ChatGPTExtensionPackage.Instance?.Configuration ?? new Configuration();

                UseSystemProxyCheckBox.IsChecked = _configuration.UseSystemProxy;
                UseCustomProxyCheckBox.IsChecked = _configuration.UseProxy;
                ProxyServerTextBox.Text = _configuration.ProxyServer;
                ProxyPortTextBox.Text = _configuration.ProxyPort.ToString();
                ProxyRequiresAuthCheckBox.IsChecked = _configuration.ProxyRequiresAuth;
                ProxyUsernameTextBox.Text = _configuration.ProxyUsername;
                ProxyPasswordBox.Password = _configuration.ProxyPassword;
                ProxyBypassTextBox.Text = _configuration.ProxyBypassList;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateUIState()
        {
            bool useSystemProxy = UseSystemProxyCheckBox.IsChecked == true;
            bool useCustomProxy = UseCustomProxyCheckBox.IsChecked == true;

            // Disable custom proxy controls if system proxy is enabled
            CustomProxyGroupBox.IsEnabled = !useSystemProxy;
            UseCustomProxyCheckBox.IsEnabled = !useSystemProxy;

            // Enable/disable proxy server settings based on custom proxy checkbox
            ProxyServerGrid.IsEnabled = useCustomProxy && !useSystemProxy;
            ProxyRequiresAuthCheckBox.IsEnabled = useCustomProxy && !useSystemProxy;

            // Enable/disable auth settings based on auth checkbox
            bool requiresAuth = ProxyRequiresAuthCheckBox.IsChecked == true;
            ProxyAuthGrid.IsEnabled = useCustomProxy && !useSystemProxy && requiresAuth;

            ProxyBypassTextBox.IsEnabled = (useCustomProxy || useSystemProxy);
        }

        private void UseSystemProxyCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (UseCustomProxyCheckBox.IsChecked == true)
            {
                UseCustomProxyCheckBox.IsChecked = false;
            }
            UpdateUIState();
        }

        private void UseSystemProxyCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateUIState();
        }

        private void UseCustomProxyCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (UseSystemProxyCheckBox.IsChecked == true)
            {
                UseSystemProxyCheckBox.IsChecked = false;
            }
            UpdateUIState();
        }

        private void UseCustomProxyCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateUIState();
        }

        private void ProxyRequiresAuthCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            UpdateUIState();
        }

        private void ProxyRequiresAuthCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateUIState();
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            TestConnectionButton.IsEnabled = false;
            TestStatusLabel.Content = "Testing...";

            try
            {
                var testConfig = GetConfigurationFromUI();
                var httpClient = CreateHttpClientWithProxy(testConfig);

                using (httpClient)
                {
                    var response = await httpClient.GetAsync("https://www.google.com", System.Threading.CancellationToken.None);
                    if (response.IsSuccessStatusCode)
                    {
                        TestStatusLabel.Content = "✓ Connection successful";
                        TestStatusLabel.Foreground = System.Windows.Media.Brushes.Green;
                    }
                    else
                    {
                        TestStatusLabel.Content = $"✗ Connection failed: {response.StatusCode}";
                        TestStatusLabel.Foreground = System.Windows.Media.Brushes.Red;
                    }
                }
            }
            catch (Exception ex)
            {
                TestStatusLabel.Content = $"✗ Connection failed: {ex.Message}";
                TestStatusLabel.Foreground = System.Windows.Media.Brushes.Red;
            }
            finally
            {
                TestConnectionButton.IsEnabled = true;
            }
        }

        private Configuration GetConfigurationFromUI()
        {
            return new Configuration
            {
                UseSystemProxy = UseSystemProxyCheckBox.IsChecked == true,
                UseProxy = UseCustomProxyCheckBox.IsChecked == true,
                ProxyServer = ProxyServerTextBox.Text,
                ProxyPort = int.TryParse(ProxyPortTextBox.Text, out int port) ? port : 8080,
                ProxyRequiresAuth = ProxyRequiresAuthCheckBox.IsChecked == true,
                ProxyUsername = ProxyUsernameTextBox.Text,
                ProxyPassword = ProxyPasswordBox.Password,
                ProxyBypassList = ProxyBypassTextBox.Text
            };
        }

        private HttpClient CreateHttpClientWithProxy(Configuration config)
        {
            var handler = new HttpClientHandler();

            if (config.UseSystemProxy)
            {
                handler.UseProxy = true;
                handler.Proxy = System.Net.WebRequest.GetSystemWebProxy();
            }
            else if (config.UseProxy && !string.IsNullOrEmpty(config.ProxyServer))
            {
                var proxy = new System.Net.WebProxy($"http://{config.ProxyServer}:{config.ProxyPort}");

                if (config.ProxyRequiresAuth && !string.IsNullOrEmpty(config.ProxyUsername))
                {
                    proxy.Credentials = new System.Net.NetworkCredential(config.ProxyUsername, config.ProxyPassword);
                }

                if (!string.IsNullOrEmpty(config.ProxyBypassList))
                {
                    var bypassList = config.ProxyBypassList.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < bypassList.Length; i++)
                    {
                        bypassList[i] = bypassList[i].Trim();
                    }
                    proxy.BypassList = bypassList;
                }

                handler.UseProxy = true;
                handler.Proxy = proxy;
            }
            else
            {
                handler.UseProxy = false;
            }

            return new HttpClient(handler);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate inputs
                if (UseCustomProxyCheckBox.IsChecked == true)
                {
                    if (string.IsNullOrWhiteSpace(ProxyServerTextBox.Text))
                    {
                        MessageBox.Show("Please enter a proxy server address.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (!int.TryParse(ProxyPortTextBox.Text, out int port) || port < 1 || port > 65535)
                    {
                        MessageBox.Show("Please enter a valid port number (1-65535).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                // Update configuration
                _configuration.UseSystemProxy = UseSystemProxyCheckBox.IsChecked == true;
                _configuration.UseProxy = UseCustomProxyCheckBox.IsChecked == true;
                _configuration.ProxyServer = ProxyServerTextBox.Text;
                _configuration.ProxyPort = int.TryParse(ProxyPortTextBox.Text, out int validPort) ? validPort : 8080;
                _configuration.ProxyRequiresAuth = ProxyRequiresAuthCheckBox.IsChecked == true;
                _configuration.ProxyUsername = ProxyUsernameTextBox.Text;
                _configuration.ProxyPassword = ProxyPasswordBox.Password;
                _configuration.ProxyBypassList = ProxyBypassTextBox.Text;

                // Save configuration
                ChatGPTExtensionPackage.Instance?.SaveConfiguration();

                MessageBox.Show("Proxy configuration saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
 
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
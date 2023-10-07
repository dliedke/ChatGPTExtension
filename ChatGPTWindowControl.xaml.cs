using System;
using System.IO;
using System.Windows;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Controls;

using EnvDTE;
using EnvDTE80;
using Microsoft.Win32;
using Newtonsoft.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.VisualStudio.Shell;

namespace ChatGPTExtension
{
    public partial class GptToolWindowControl : UserControl
    {
        #region Web IDs and URLs

        private const string CHAT_GPT_URL = "https://chat.openai.com/";
        private const string GPT_PROMPT_TEXT_AREA_ID = "prompt-textarea";
        private const string GPT_SELECTOR_COPY_BUTTON = @"button.flex.ml-auto.gizmo\\:ml-0.gap-2.items-center:not([data-listener-added])";
        private const string GPT_COPY_CODE_BUTTON_TEXT = "Copy code";

        #endregion

        #region Constructor and Initialization

        private bool _enableCopyCode = true;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD110:Observe result of async calls", Justification = "<Pending>")]
        public GptToolWindowControl()
        {
            InitializeComponent();

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            InitializeAsync();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        private async Task InitializeAsync()
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Create user path for the Edge WebView2 profile
                string userDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChatGPTExtension", "WebView2");
                if (!Directory.Exists(userDataPath))
                {
                    Directory.CreateDirectory(userDataPath);
                }

                // Retrieve the Edge WebView2 directory
                string edgeWebView2Path = GetEdgeWebView2Path();

                // Set webview 2 path and user data path for the browser
                CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(edgeWebView2Path, userDataPath);
                await webView.EnsureCoreWebView2Async(environment);

                // Open Chat GPT
                webView.Source = new Uri(CHAT_GPT_URL);

                // WebMessageReceived to receive events from browser in this extension
                webView.WebMessageReceived += WebView_WebMessageReceived;

                // If the GPT prompt appers, already call AddHandlerCopyCodeAsync()
                await WaitForElementAsync(GPT_PROMPT_TEXT_AREA_ID);

                // Timer to inject JS code to detect clicks in "Copy code" button in Chat GPT
                StartTimer();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error in InitializeAsync(): " + ex.Message);
            }
        }

        public string GetEdgeWebView2Path()
        {
            // Retrieve the Edge WebView2 path from registry
            string keyPath = @"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{2CD8A007-E189-409D-A2C8-9AF4EF3C72AA}";
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath))
            {
                if (key != null)
                {
                    object pathObject = key.GetValue("pv");
                    if (pathObject != null)
                    {
                        return pathObject.ToString();
                    }
                }
            }
            return null;
        }

        private async Task WaitForElementAsync(string elementId)
        {
            bool elementFound = false;
            while (!elementFound)
            {
                string script = $"document.getElementById('{elementId}') ? 'found' : 'notfound';";
                var result = await webView.ExecuteScriptAsync(script);
                if (result == "\"found\"") // Note the extra quotes, ExecuteScriptAsync returns JSON serialized strings.
                {
                    elementFound = true;
                }
                else
                {
                    await Task.Delay(500); // Wait for half a second before checking again.
                }
            }
            await AddHandlerCopyCodeAsync();
        }


        #endregion

        #region VS.NET to GPT

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD110:Observe result of async calls", Justification = "<Pending>")]
        private void OnSendCodeButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Button btn = sender as Button;
                string extraCommand = btn.Tag as string;

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                SendSelectedCodeToGPTAsync(extraCommand);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error in OnSendCodeButtonClick(): " + ex.Message);
            }
        }

        private string GetSelectedCodeFromVS()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Retrieve code selected from VS.NET
            DTE2 dte = (DTE2)Package.GetGlobalService(typeof(DTE));
            TextDocument activeDoc = (TextDocument)dte.ActiveDocument.Object("TextDocument");
            TextSelection selection = activeDoc.Selection;

            return selection.Text;
        }

        private async Task SendSelectedCodeToGPTAsync(string extraCommand)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Place code in GPT prompt text, not overriding existing text
            // Also sends the event to enable the send message button in GPT
            string selectedCode = GetSelectedCodeFromVS();

            // No code to process
            if (string.IsNullOrEmpty(selectedCode))
            {
                return;
            }

            // In case we have extra command
            if (!string.IsNullOrEmpty(extraCommand))
            {
                selectedCode = extraCommand + "\r\n" + selectedCode;
            }

            string script = $@"var existingText = document.getElementById('{GPT_PROMPT_TEXT_AREA_ID}').value;
                               document.getElementById('{GPT_PROMPT_TEXT_AREA_ID}').value = existingText + {JsonConvert.SerializeObject(selectedCode)};
        
                               var inputEvent = new Event('input', {{
                                   'bubbles': true,
                                   'cancelable': true
                               }});
                               document.getElementById('{GPT_PROMPT_TEXT_AREA_ID}').dispatchEvent(inputEvent); ";

            await webView.CoreWebView2.ExecuteScriptAsync(script);

            // In case we have extra command send the prompt automatically
            if (!string.IsNullOrEmpty(extraCommand))
            {
                await SubmitPromptGPTAsync();
            }
        }

        private async Task<string> GetSelectedTextFromGPTAsync()
        {
            // Retrieve selected text in GPT
            string script = @"window.getSelection().toString()";
            string selectedText = await webView.CoreWebView2.ExecuteScriptAsync(script);

            // Convert returned JSON string to a regular string.
            return JsonConvert.DeserializeObject<string>(selectedText);
        }

        private void InsertTextIntoVS(string text)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Get the selected text in VS.NET
            DTE2 dte = (DTE2)Package.GetGlobalService(typeof(DTE));
            TextDocument activeDoc = (TextDocument)dte.ActiveDocument.Object("TextDocument");
            TextSelection selection = activeDoc.Selection;

            // This will replace the current selection in VS.NET with the text.
            // If nothing is selected, it'll just insert the text at the current cursor position.
            selection.Insert(text);
        }

        #endregion

        #region GPT to VS.NET

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD110:Observe result of async calls", Justification = "<Pending>")]
        private void OnReceiveCodeButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                OnTransferTextFromGPTtoVSAsync();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error in OnReceiveCodeButtonClick(): " + ex.Message);
            }
        }


        private async Task OnTransferTextFromGPTtoVSAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Retrieve selected code in GPT, insert and format in VS.NET
            string textFromBrowser = await GetSelectedTextFromGPTAsync();

            // If we have text selected in browser send to VS.NET
            if (!string.IsNullOrEmpty(textFromBrowser))
            {
                InsertTextIntoVS(textFromBrowser);
                FormatTextInVS();
            }
        }

        private void FormatTextInVS()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Format code in VS.NET if sintax is correct
            DTE2 dte = (DTE2)Package.GetGlobalService(typeof(DTE));
            if (dte.ActiveDocument != null)
            {
                dte.ActiveDocument.Activate();
                try
                {
                    dte.ExecuteCommand("Edit.FormatDocument");
                }
                catch (Exception ex)
                {
                    // Handle or log the exception
                    Debug.WriteLine("Error in FormatTextInVS: " + ex.Message);
                }
            }
        }

        #endregion

        #region Timer to add handler from GPT click events to VS.NET

        private async Task AddHandlerCopyCodeAsync()
        {
            try
            {
                // Make sure to capture enter key without shift to send the prompt to GPT later
                // This is due to issue that only shift-enter is being send in WebView2 even when typing enter
                string script = @"if (!document.body.hasAttribute('data-keydown-listener-added')) {
                                    document.addEventListener('keydown', function(event) {

                                        if (event.keyCode == 13 && !event.shiftKey)
                                            window.chrome.webview.postMessage('EnterPressed');
                                    });
                                    document.body.setAttribute('data-keydown-listener-added', 'true');
                                }
                                ";

                await webView.ExecuteScriptAsync(script);

                // Copy code button handler for GPT
                string addEventListenersScript = $@"var buttons = Array.from(document.querySelectorAll('{GPT_SELECTOR_COPY_BUTTON}')).filter(button => button.textContent.includes('{GPT_COPY_CODE_BUTTON_TEXT}'));
                                                   buttons.forEach(function(button) {{
                                                       button.addEventListener('click', function() {{
                                                           window.chrome.webview.postMessage('CopyCodeButtonClicked');
                                                       }});
                                                       // Mark the button as having the listener added
                                                       button.setAttribute('data-listener-added', 'true');
                                                   }});
                                                   ";

                await webView.ExecuteScriptAsync(addEventListenersScript);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error in AddHanlderCopyCode(): " + ex.Message);
            }
        }

        private async Task SubmitPromptGPTAsync()
        {
            // Submit the GPT prompt
            string script = "document.querySelector('[data-testid=\"send-button\"]').click();";
            await webView.ExecuteScriptAsync(script);

            // Wait a bit
            await Task.Delay(2000);

            // Click in the scroll to bottom button
            string initialScript = "var button = document.querySelector('.cursor-pointer.absolute.right-6'); if (button) { button.click(); }";
            await webView.ExecuteScriptAsync(initialScript);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "<Pending>")]
        private async void WebView_WebMessageReceived(object sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // When GPT Copy code button was clicked, automatically insert the code in VS.NET
                if (e.TryGetWebMessageAsString() == "CopyCodeButtonClicked" && _enableCopyCode)
                {
                    // Using async delay instead of freezing the thread
                    await Task.Delay(1000);

                    // Handle the button click event here
                    string textFromClipboard = Clipboard.GetText();
                    InsertTextIntoVS(textFromClipboard);
                    FormatTextInVS();
                }

                if (e.TryGetWebMessageAsString() == "EnterPressed")
                {
                    await SubmitPromptGPTAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error in WebView_WebMessageReceived(): " + ex.Message);
            }
        }

        private System.Timers.Timer timer;

        private void StartTimer()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                StopTimer();

                timer = new System.Timers.Timer(5000);
                timer.Elapsed += HandleTimerElapsed;
                timer.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error in StartTimer(): " + ex.Message);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "<Pending>")]
        private async void HandleTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await AddHandlerCopyCodeAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error in HandleTimerElapsed(): " + ex.Message);
            }
        }

        public void Dispose()
        {
            StopTimer();
        }

        private void StopTimer()
        {
            timer?.Stop();
        }

        #endregion

        private void EnableCopyCodeCheckBox_Click(object sender, RoutedEventArgs e)
        {
            _enableCopyCode = EnableCopyCodeCheckBox.IsChecked.Value;
        }
    }
}
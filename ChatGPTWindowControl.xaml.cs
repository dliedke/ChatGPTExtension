using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Diagnostics;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using EnvDTE;
using EnvDTE80;
using Newtonsoft.Json;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;
using Microsoft.VisualStudio.Shell;

using System.Windows.Interop;

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
        private readonly IServiceProvider _serviceProvider;
        private ConfigurationWindow _configWindow = new ConfigurationWindow();
        private DTE2 _dte;
        private Events _events;
        private WindowEvents _windowEvents;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD110:Observe result of async calls", Justification = "<Pending>")]
        public GptToolWindowControl(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;

            InitializeComponent();

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            InitializeAsync();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            LoadContextMenuActions();
        }

        private async Task InitializeAsync()
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                _dte = Package.GetGlobalService(typeof(DTE)) as DTE2;

                if (_dte != null)
                {
                    _events = _dte.Events;
                    _windowEvents = _events.WindowEvents;
                    _windowEvents.WindowActivated += OnWindowActivated;
                }


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

                // Remove event handlers when control is unloaded
                Unloaded += OnControlUnloaded;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error in InitializeAsync(): " + ex.Message);
            }
        }

        private void OnControlUnloaded(object sender, RoutedEventArgs e)
        {
            if (_windowEvents != null)
            {
                // Unsubscribe from events to prevent memory leaks.
                // Unfortunately, the EnvDTE API doesn't provide direct unsubscription methods.
                // We'll set our references to null to let the GC do the cleanup.
                _windowEvents = null;
                _events = null;
                _dte = null;
            }
        }

        private void OnWindowActivated(EnvDTE.Window GotFocus, EnvDTE.Window LostFocus)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Check if the activated window is our tool window
            if (GotFocus.Caption == "Chat GPT Extension")
            {
                webView.Visibility = Visibility.Visible;
            }
            else
            {
                // If any of the tool windows from the bottom in VS.NET 
                // are selected, then hide the webview to not conflict
                if (GotFocus.Caption == "Output" ||
                    GotFocus.Caption.StartsWith("Error List") ||
                    GotFocus.Caption.StartsWith("Task List") ||
                    GotFocus.Caption.StartsWith("PowerShell ") ||
                    GotFocus.Caption.StartsWith("Developer ") ||
                    GotFocus.Caption.StartsWith("Find ") ||
                    GotFocus.Caption == "Exception Settings" ||
                    GotFocus.Caption == "Package Manager Console" ||
                    GotFocus.Caption == "CodeLens" ||
                    GotFocus.Caption == "Bookmarks" ||
                    GotFocus.Caption == "Call Hierarchy" ||
                    GotFocus.Caption == "Code Definition Window")
                {
                    webView.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // For code windows and other windows we will display the webview
                    webView.Visibility = Visibility.Visible;
                }
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
            EnvDTE.TextSelection selection = activeDoc.Selection;

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
                // Get language of the file
                string activeLanguage = GetActiveFileLanguage();

                // Replace {languageCode} with correct programming language
                selectedCode = extraCommand.Replace("{languageCode}", activeLanguage) + "\r\n" + selectedCode;

                // Replace the full prompt in GPT to send a new one
                string script = $@"document.getElementById('{GPT_PROMPT_TEXT_AREA_ID}').value = {JsonConvert.SerializeObject(selectedCode)};
        
                               var inputEvent = new Event('input', {{
                                   'bubbles': true,
                                   'cancelable': true
                               }});
                               document.getElementById('{GPT_PROMPT_TEXT_AREA_ID}').dispatchEvent(inputEvent); ";

                await webView.CoreWebView2.ExecuteScriptAsync(script);
            }
            else
            {
                // Keep existing prompt and add code from VS.NET
                string script = $@"var existingText = document.getElementById('{GPT_PROMPT_TEXT_AREA_ID}').value;
                               document.getElementById('{GPT_PROMPT_TEXT_AREA_ID}').value = existingText + '\r\n' + {JsonConvert.SerializeObject(selectedCode)};
        
                               var inputEvent = new Event('input', {{
                                   'bubbles': true,
                                   'cancelable': true
                               }});
                               document.getElementById('{GPT_PROMPT_TEXT_AREA_ID}').dispatchEvent(inputEvent); ";

                await webView.CoreWebView2.ExecuteScriptAsync(script);
            }


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
            EnvDTE.TextSelection selection = activeDoc.Selection;

            // This will replace the current selection in VS.NET with the text.
            // If nothing is selected, it'll just insert the text at the current cursor position.
            selection.Insert(text);

            // Set focus back to the active document
            dte.ActiveDocument.Activate();
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
                FormatCodeInVS();
            }
        }

        private void FormatCodeInVS()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Format code in VS.NET if syntax is correct
            DTE2 dte = (DTE2)Package.GetGlobalService(typeof(DTE));
            if (dte.ActiveDocument != null)
            {
                // List of extensions for code files supported by Visual Studio
                string[] supportedExtensions =
                {
                    ".cs",    // C#
                    ".vb",    // Visual Basic
                    ".cpp",   // C++
                    ".h",     // C++ header
                    ".c",     // C
                    ".fs",    // F#
                    ".fsx",   // F# script
                    ".ts",    // TypeScript
                    ".js",    // JavaScript
                    ".py",    // Python
                    ".xaml"   // WPF
                };

                // Check the file extension of the active document
                string fileExtension = System.IO.Path.GetExtension(dte.ActiveDocument.FullName).ToLower();

                if (supportedExtensions.Contains(fileExtension))
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
        }

        #endregion

        #region Timer to add handler from GPT click events to VS.NET

        private void EnableCopyCodeCheckBox_Click(object sender, RoutedEventArgs e)
        {
            _enableCopyCode = EnableCopyCodeCheckBox.IsChecked.Value;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "<Pending>")]
        private async void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            string cursorPositionScript = "";

            if (e.Key == Key.Home || e.Key == Key.End)
            {
                e.Handled = true; // Cancel the default behavior

                if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) // Shift is pressed
                {
                    if (e.Key == Key.Home)
                    {
                        cursorPositionScript = @"
                            var textbox = document.getElementById('" + GPT_PROMPT_TEXT_AREA_ID + @"');
                            var end = textbox.selectionEnd; 
                            textbox.setSelectionRange(0, end);";
                    }
                    else if (e.Key == Key.End)
                    {
                        cursorPositionScript = @"
                            var textbox = document.getElementById('" + GPT_PROMPT_TEXT_AREA_ID + @"');
                            var start = textbox.selectionStart;
                            textbox.setSelectionRange(start, textbox.value.length);";
                    }
                }
                else // Shift is NOT pressed
                {
                    if (e.Key == Key.Home)
                    {
                        cursorPositionScript = $"document.getElementById('{GPT_PROMPT_TEXT_AREA_ID}').setSelectionRange(0, 0);"; // This moves the cursor to the start of the textarea for HOME without shift
                    }
                    else if (e.Key == Key.End)
                    {
                        cursorPositionScript = $"var textbox = document.getElementById('{GPT_PROMPT_TEXT_AREA_ID}'); textbox.setSelectionRange(textbox.value.length, textbox.value.length);"; // This moves the cursor to the end of the textarea for END without shift
                    }
                }

                if (!string.IsNullOrEmpty(cursorPositionScript))
                {
                    await webView.ExecuteScriptAsync(cursorPositionScript);
                }
            }
        }



        private async Task AddHandlerCopyCodeAsync()
        {
            try
            {
                // Capture Enter, Home and End keys that should
                // be handled different in this extension to work
                // with Chat GPT
                string script = @"if (!document.body.hasAttribute('data-keydown-listener-added')) {
                                        document.addEventListener('keydown', function(event) {
                                            // Check for Enter without Shift key
                                            if (event.keyCode == 13 && !event.shiftKey) {
                                                window.chrome.webview.postMessage('EnterPressed');
                                            }
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
            await Task.Delay(1000);

            // Click in the scroll to bottom button
            string initialScript = "var button = document.querySelector('.cursor-pointer.absolute.right-6'); if (button) { button.click(); }";
            await webView.ExecuteScriptAsync(initialScript);

            // Wait a bit
            await Task.Delay(2000);

            // Click in the scroll to bottom button
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
                    FormatCodeInVS();
                }

                var webMessage = e.TryGetWebMessageAsString();

                switch (webMessage)
                {
                    // Handle enter key to send the prompt to GPT
                    case "EnterPressed":
                        await SubmitPromptGPTAsync();
                        break;
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

        #region Other Actions for GPT to process and Reload GPT

#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void OnCompleteCodeButtonClick(object sender, RoutedEventArgs e)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            try
            {
                // Prompt to show full complete code in GPT
                string promptCompleteCode = "Please show full complete code with complete methods implementation without any placeholders like ... or assuming code segments";

                // Replace the full prompt in GPT to send a new one
                string script = $@"document.getElementById('{GPT_PROMPT_TEXT_AREA_ID}').value = '{promptCompleteCode}';
        
                               var inputEvent = new Event('input', {{
                                   'bubbles': true,
                                   'cancelable': true
                               }});
                               document.getElementById('{GPT_PROMPT_TEXT_AREA_ID}').dispatchEvent(inputEvent); ";

                await webView.CoreWebView2.ExecuteScriptAsync(script);

                // Also submit the prompt
                await SubmitPromptGPTAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error in OnCompleteCodeButtonClick(): " + ex.Message);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "<Pending>")]
        private async void OnSendCodeMenuItemClick(object sender, RoutedEventArgs e)
        {
            try
            {
                // Process other actions for GPT
                if (sender is MenuItem menuItem && menuItem.Tag is string command)
                {
                    await SendSelectedCodeToGPTAsync(command);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error in OnSendCodeMenuItemClick(): " + ex.Message);
            }
        }

        private void OnReloadChatGptItemClick(object sender, RoutedEventArgs e)
        {
            try
            {
                // Set page as blank
                webView.Source = new Uri("about:blank");

                // Open Chat GPT again
                webView.Source = new Uri(CHAT_GPT_URL);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error in OnReloadChatGptItemClick(): " + ex.Message);
            }
        }

        #endregion

        #region Find out language of the active document

        private string GetActiveFileLanguage()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_serviceProvider == null)
                return string.Empty;

            DTE dte = (DTE)_serviceProvider.GetService(typeof(DTE));
            if (dte != null && dte.ActiveDocument != null)
            {
                string fileExtension = System.IO.Path.GetExtension(dte.ActiveDocument.FullName).ToLower();

                // Dictionary mapping file extensions to language descriptions.
                Dictionary<string, string> extensionToLanguageMap = new Dictionary<string, string>
                {
                    {".cs", "C#"},
                    {".vb", "Visual Basic"},
                    {".cpp", "C++"},
                    {".h", "C++ header"},
                    {".c", "C"},
                    {".fs", "F#"},
                    {".fsx", "F# script"},
                    {".ts", "TypeScript"},
                    {".js", "JavaScript"},
                    {".py", "Python"},
                };

                if (extensionToLanguageMap.TryGetValue(fileExtension, out string language))
                {
                    return language;
                }
            }

            return string.Empty;  // If no match is found or if there's no active document.
        }

        #endregion

        #region Configurable Prompts

        private void ConfigureExtensionMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Show configure window
            _configWindow.ShowDialog();

            // Recreate the window and load the actions
            _configWindow = new ConfigurationWindow();
            LoadContextMenuActions();
        }

        private void LoadContextMenuActions()
        {
            var actions = _configWindow.ActionItems;

            CodeActionsContextMenu.Items.Clear();

            // Add all configured actions/prompts in the context menu
            foreach (var action in actions)
            {
                var name = action.Name;
                var prompt = action.Prompt;

                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(prompt))
                {
                    var formattedPrompt = prompt.Replace(" {languageCode}", string.Empty);

                    var menuItem = new MenuItem
                    {
                        Header = name,
                        Tag = formattedPrompt
                    };

                    menuItem.Click += OnSendCodeMenuItemClick;
                    CodeActionsContextMenu.Items.Add(menuItem);
                }
            }

            // Add a separator line
            CodeActionsContextMenu.Items.Add(new Separator());

            // Add Reload Chat GPT menu item
            var reloadMenuItem = new MenuItem { Header = "Reload Chat GPT..." };
            reloadMenuItem.Click += OnReloadChatGptItemClick;
            CodeActionsContextMenu.Items.Add(reloadMenuItem);

            // Add Configure extension... menu item
            var configureMenuItem = new MenuItem { Header = "Configure extension..." };
            configureMenuItem.Click += ConfigureExtensionMenuItem_Click;
            CodeActionsContextMenu.Items.Add(configureMenuItem);
        }

        #endregion
    }

    public class WindowZOrder
    {
        private const uint GW_HWNDNEXT = 2;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        public static bool IsWindowTopmost(System.Windows.Window window)
        {
            IntPtr hWnd = new WindowInteropHelper(window).Handle;
            IntPtr currentHwnd = hWnd;

            while (true)
            {
                currentHwnd = GetWindow(currentHwnd, GW_HWNDNEXT);
                if (currentHwnd == IntPtr.Zero)
                    break;

                if (IsWindowVisible(currentHwnd))
                    return false; // There's another visible window on top
            }

            return true; // The provided window is the topmost visible window
        }
    }
}
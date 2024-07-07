/* *******************************************************************************************************************
 * Application: ChatGPTExtension
 * 
 * Autor:  Daniel Liedke
 * 
 * Copyright © Daniel Liedke 2024
 * Usage and reproduction in any manner whatsoever without the written permission of Daniel Liedke is strictly forbidden.
 *  
 * Purpose: Main user control for the Chat GPT/Gemini/Claude extension for VS.NET 2022
 *           
 * *******************************************************************************************************************/

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
using Microsoft.VisualStudio.Threading;
using System.Windows.Threading;
using Window = System.Windows.Window;
using Microsoft.VisualStudio.Shell.Interop;

namespace ChatGPTExtension
{
    public partial class GptToolWindowControl : UserControl
    {
        #region Web IDs and URLs for GPT and Gemini

        // Note: There are ids also in method SubmitPromptAIAsync()

        // IDs and selectors might need updates in new GPT versions
        private const string CHAT_GPT_URL = "https://chatgpt.com/";
        private const string GPT_PROMPT_TEXT_AREA_ID = "prompt-textarea";
        private const string GPT_COPY_CODE_BUTTON_SELECTOR = "button.flex.gap-1.items-center";
        private const string GPT_COPY_CODE_BUTTON_ICON_SELECTOR = "button.flex.gap-1.items-center svg.icon-sm";

        // Selectors might need updates in new Gemini versions
        private const string GEMINI_URL = "https://gemini.google.com";
        private const string GEMINI_PROMPT_CLASS = "ql-editor";
        private const string GEMINI_COPY_CODE_BUTTON_CLASS = "copy-button";

        // Selectors might need updates in new Claude versions
        private const string CLAUDE_URL = "https://claude.ai/chats";
        private const string CLAUDE_PROMPT_CLASS = "ProseMirror";
        private const string CLAUDE_COPY_CODE_BUTTON_TEXT = "Copy";
        private const string CLAUDE_PROJECT_COPY_CODE_BUTTON_SELECTOR = "button.inline-flex[data-state=\"closed\"] svg path[d=\"M200,32H163.74a47.92,47.92,0,0,0-71.48,0H56A16,16,0,0,0,40,48V216a16,16,0,0,0,16,16H200a16,16,0,0,0,16-16V48A16,16,0,0,0,200,32Zm-72,0a32,32,0,0,1,32,32H96A32,32,0,0,1,128,32Zm72,184H56V48H82.75A47.93,47.93,0,0,0,80,64v8a8,8,0,0,0,8,8h80a8,8,0,0,0,8-8V64a47.93,47.93,0,0,0-2.75-16H200Z\"]";

        #endregion

        #region Constructor and Initialization

        private DTE2 _dte;
        private Events _events;
        private WindowEvents _windowEvents;
        private bool _enableCopyCode = true;
        private readonly IServiceProvider _serviceProvider;
        private ConfigurationWindow _configWindow = new ConfigurationWindow();
        private AIModelType _aiModelType = AIModelType.GPT;
        private ChatGPTToolWindow _parentToolWindow;

        public enum AIModelType : int
        {
            GPT = 1,
            Gemini = 2,
            Claude = 3
        }


        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD110:Observe result of async calls", Justification = "<Pending>")]
        public GptToolWindowControl(IServiceProvider serviceProvider, ChatGPTToolWindow parent)
        {
            _parentToolWindow = parent;
            _serviceProvider = serviceProvider;

            // Initialize the JoinableTaskFactory
            _joinableTaskFactory = ThreadHelper.JoinableTaskFactory;

            InitializeComponent();

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            InitializeAsync();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            _aiModelType = LoadConfiguration();
            LoadContextMenuActions();
            _parentToolWindow = parent;

            AddMinimizeRestoreMenuItem();


            // Initialize WindowHelper
            var dte = serviceProvider.GetService(typeof(DTE)) as DTE2;
            _windowHelper = new WindowHelper(dte);

            // Set up a timer to periodically check the window state
            _updateTimer = new DispatcherTimer();
            _updateTimer.Interval = TimeSpan.FromMilliseconds(500);
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();
        }


        private async Task InitializeAsync()
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Retrieves DTE and WindowsActivated event
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

                if (_aiModelType == AIModelType.GPT)
                {
                    // Open Chat GPT
                    webView.Source = new Uri(CHAT_GPT_URL);
                }
                if (_aiModelType == AIModelType.Gemini)
                {
                    // Open Gemini
                    webView.Source = new Uri(GEMINI_URL);
                }
                if (_aiModelType == AIModelType.Claude)
                {
                    // Open Claude
                    webView.Source = new Uri(CLAUDE_URL);
                }

                // WebMessageReceived to receive events from browser in this extension
                webView.WebMessageReceived += WebView_WebMessageReceived;

                // Timer to inject JS code to detect clicks in "Copy code" button in Chat GPT
                await _joinableTaskFactory.SwitchToMainThreadAsync();
                await StartTimerAsync();

                // Start monitoring the timer status
                _ = CheckTimerStatusAsync();

                // If the AI prompt appers, already call AddHandlerCopyCodeAsync()
                if (_aiModelType == AIModelType.GPT)
                {
                    await WaitForElementByIdAsync(GPT_PROMPT_TEXT_AREA_ID);
                }
                if (_aiModelType == AIModelType.Gemini)
                {
                    await WaitForElementByClassAsync(GEMINI_PROMPT_CLASS);
                }
                if (_aiModelType == AIModelType.Claude)
                {
                    await WaitForElementByClassAsync(CLAUDE_PROMPT_CLASS);
                }

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
            if (timer != null)
            {
                timer.Stop();
            }

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
            if (GotFocus.Caption == "Chat GPT Extension" || GotFocus.Caption == "Gemini Extension")
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

        private async Task WaitForElementByIdAsync(string elementId)
        {
            bool elementFound = false;
            while (!elementFound)
            {
                string script = $"document.getElementById('{elementId}') ? 'found' : 'notfound';";
                var result = await webView.ExecuteScriptAsync(script);
                if (result == "\"found\"") // Note the extra quotes, ExecuteScriptAsync returns JSON serialized strings.
                {
                    elementFound = true;

                    // Run GPT wide script is available
                    string gptWideScript = GPTWideWindow.GetGPTWideScript();
                    if (!string.IsNullOrEmpty(gptWideScript))
                    {
                        await webView.ExecuteScriptAsync(gptWideScript);
                    }
                }
                else
                {
                    await Task.Delay(500); // Wait for half a second before checking again.
                }
            }
            await AddHandlerCopyCodeAsync();
        }

        private async Task WaitForElementByClassAsync(string className)
        {
            bool elementFound = false;
            while (!elementFound)
            {
                // Use querySelector with an attribute selector to find the element by its data-placeholder value.
                string script = $"document.querySelector('.{className}') ? 'found' : 'notfound';";
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
            await AddHandlerCopyCodeAsync(); // Ensure this method is implemented to add the required handler.
        }

        #endregion

        #region VS.NET to GPT/Gemini

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD110:Observe result of async calls", Justification = "<Pending>")]
        private void OnSendCodeButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                Button btn = sender as Button;
                string extraCommand = btn.Tag as string;

                // If button is btnVSNETToAI and _isMinimized is true, then restore the window
                if (extraCommand == "btnVSNETToAI" && _isMinimized)
                {
                    RestoreWindow();
                    return;
                }

                // If button is btnVSNETToAI then clear extraCommand
                if (extraCommand == "btnVSNETToAI")
                {
                    extraCommand = string.Empty;
                }

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                SendSelectedCodeToAIAsync(extraCommand);
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

        private async Task SendSelectedCodeToAIAsync(string extraCommand)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Place code in GPT prompt text, not overriding existing text
            // Also sends the event to enable the send message button in GPT
            string selectedCode = GetSelectedCodeFromVS();

            // Check if there is a file attached from this extension
            bool attachedFile = false;
            attachedFile = await IsFileAttachedAsync();

            // If there is no selected code from VS.NET and we have attached file
            // Then set the selected code to "(attached)"
            if (string.IsNullOrEmpty(selectedCode) && attachedFile)
            {
                selectedCode = "(attached)";
            }

            // No code to process
            if (string.IsNullOrEmpty(selectedCode))
            {
                return;
            }

            if (!string.IsNullOrEmpty(extraCommand))
            {
                // Get language of the file
                string activeLanguage = GetActiveFileLanguage();

                // Replace {languageCode} with correct programming language
                selectedCode = extraCommand.Replace("{languageCode}", activeLanguage) + "\r\n" + selectedCode;

                // Replace the full prompt in GPT/Gemini to send a new one
                string script = string.Empty;

                if (_aiModelType == AIModelType.GPT)
                {
                    script = $@"var element = document.getElementById('{GPT_PROMPT_TEXT_AREA_ID}');
                                element.value = {JsonConvert.SerializeObject(selectedCode)};
               
                                var inputEvent = new Event('input', {{
                                    'bubbles': true,
                                    'cancelable': true
                                }});
                                element.dispatchEvent(inputEvent);";
                }

                if (_aiModelType == AIModelType.Gemini)
                {
                    script = GetScriptGeminiReceiveCode(selectedCode);
                }

                if (_aiModelType == AIModelType.Claude)
                {
                    var lines = selectedCode.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                    var codeHtml = string.Join("", lines.Select(line => $"<p>{line}</p>"));

                    script = $@"var element = document.querySelector('.{CLAUDE_PROMPT_CLASS}');
                                element.innerHTML = {JsonConvert.SerializeObject(codeHtml)};

                                var inputEvent = new Event('input', {{
                                'bubbles': true,
                                'cancelable': true
                                }});

                                element.dispatchEvent(inputEvent);";
                }

                await webView.CoreWebView2.ExecuteScriptAsync(script);
            }
            else
            {
                // Keep existing prompt and add code from VS.NET
                string script = string.Empty;

                if (_aiModelType == AIModelType.GPT)
                {
                    script = $@"var existingText = document.getElementById('{GPT_PROMPT_TEXT_AREA_ID}').value;
                                document.getElementById('{GPT_PROMPT_TEXT_AREA_ID}').value = existingText + '\r\n' + {JsonConvert.SerializeObject(selectedCode)};
               
                                var inputEvent = new Event('input', {{
                                    'bubbles': true,
                                    'cancelable': true
                                }});
                                document.getElementById('{GPT_PROMPT_TEXT_AREA_ID}').dispatchEvent(inputEvent);";
                }

                if (_aiModelType == AIModelType.Gemini)
                {
                    script = GetScriptGeminiReceiveCode(selectedCode);
                }


                if (_aiModelType == AIModelType.Claude)
                {
                    var lines = selectedCode.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                    var codeHtml = string.Join("", lines.Select(line => $"<p>{line}</p>"));

                    script = $@"var elements = document.querySelectorAll('.{CLAUDE_PROMPT_CLASS}');
                    var index = elements.length > 1 ? 1 : 0;
                    var existingHtml = elements[index].innerHTML;
                    elements[index].innerHTML = existingHtml + '<p></p>' + {JsonConvert.SerializeObject(codeHtml)};

                    var inputEvent = new Event('input', {{
                    'bubbles': true,
                    'cancelable': true
                    }});

                    elements[index].dispatchEvent(inputEvent);";
                }

                await webView.CoreWebView2.ExecuteScriptAsync(script);
            }

            // In case we have extra command send the prompt automatically
            if (!string.IsNullOrEmpty(extraCommand))
            {
                await SubmitPromptAIAsync();
            }
        }

        private static string GetScriptGeminiReceiveCode(string selectedCode)
        {
            var lines = selectedCode.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var codeHtml = string.Join("", lines.Select(line => $"<p>{line}</p>"));

            // Serialize the HTML for JavaScript
            var codeHtmlJson = JsonConvert.SerializeObject(codeHtml);

            var script = $@"
                    (function() {{
                        // Check if the policy already exists and create it if it doesn't
                        if (!window.myTrustedTypesPolicy) {{
                            window.myTrustedTypesPolicy = trustedTypes.createPolicy('default', {{
                                createHTML: (string) => string
                            }});
                        }}

                        // Use the existing policy to create TrustedHTML
                        const trustedHTML = window.myTrustedTypesPolicy.createHTML({codeHtmlJson});

                        // Assign TrustedHTML to innerHTML by appending it
                        var element = document.querySelector('.{GEMINI_PROMPT_CLASS}');
                        element.innerHTML = element.innerHTML + trustedHTML;

                        // Dispatch the input event
                        var inputEvent = new Event('input', {{
                            'bubbles': true,
                            'cancelable': true
                        }});
                        element.dispatchEvent(inputEvent);
                    }})();
                ";

            return script;
        }

        private async Task<string> GetSelectedTextFromAIAsync()
        {
            // Retrieve selected text in GPT/Gemini
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

        #region GPT/Gemini to VS.NET

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
            string textFromBrowser = await GetSelectedTextFromAIAsync();

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
                    ".xaml"   // WPF UI
                };

                // Check the file extension of the active document
                string fileExtension = System.IO.Path.GetExtension(dte.ActiveDocument.FullName).ToLower();

                if (supportedExtensions.Contains(fileExtension))
                {
                    dte.ActiveDocument.Activate();
                    try
                    {
                        // Same as CTRL+K+D
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

        #region Timer to add handler from GPT/Gemini click events to VS.NET

        private void EnableCopyCodeCheckBox_Click(object sender, RoutedEventArgs e)
        {
            // Save if enable copy code is checked
            _enableCopyCode = EnableCopyCodeCheckBox.IsChecked.Value;
        }

        /// <summary>
        /// This method will fix issues with HOME, END and SHIFT+HOME, SHIFT+END keys
        /// in the AI prompt text area
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "<Pending>")]
        private async void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // GPT
            if (_aiModelType == AIModelType.GPT)
            {
                string cursorPositionScript = "";

                // If Home or End is pressed
                if (e.Key == Key.Home || e.Key == Key.End)
                {
                    e.Handled = true; // Cancel the default behavior

                    // JavaScript to find the start and end of the current line
                    string findLineBoundsScript = @"
                            var textbox = document.getElementById('" + GPT_PROMPT_TEXT_AREA_ID + @"');
                            var value = textbox.value;
                            var start = textbox.selectionStart;
                            var end = textbox.selectionEnd;
            
                            while(start > 0 && value[start-1] != '\n') { start--; }
                            while(end < value.length && value[end] != '\n') { end++; }
            
                            [start, end];";  // This will give the start and end positions of the current line

                    var result = await webView.ExecuteScriptAsync(findLineBoundsScript);
                    var bounds = JsonConvert.DeserializeObject<int[]>(result);
                    int startOfLine = bounds[0];
                    int endOfLine = bounds[1];

                    // If Shift is pressed
                    if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                    {
                        // If Home is pressed
                        if (e.Key == Key.Home)
                        {
                            cursorPositionScript = @"
                            var textbox = document.getElementById('" + GPT_PROMPT_TEXT_AREA_ID + @"');
                            var currentEnd = textbox.selectionEnd; 
                            textbox.setSelectionRange(" + startOfLine + @", currentEnd);";
                        }
                        // If End is pressed
                        else if (e.Key == Key.End)
                        {
                            cursorPositionScript = @"
                            var textbox = document.getElementById('" + GPT_PROMPT_TEXT_AREA_ID + @"');
                            var currentStart = textbox.selectionStart;
                            textbox.setSelectionRange(currentStart, " + endOfLine + @");";
                        }
                    }
                    else // Shift is NOT pressed
                    {
                        // If Home or End is pressed
                        if (e.Key == Key.Home)
                        {
                            cursorPositionScript = @"
                            var textbox = document.getElementById('" + GPT_PROMPT_TEXT_AREA_ID + @"');
                            textbox.setSelectionRange(" + startOfLine + @", " + startOfLine + @");";
                        }
                        else if (e.Key == Key.End)
                        {
                            cursorPositionScript = @"
                            var textbox = document.getElementById('" + GPT_PROMPT_TEXT_AREA_ID + @"');
                            textbox.setSelectionRange(" + endOfLine + @", " + endOfLine + @");";
                        }
                    }

                    // Execute javascript script code
                    if (!string.IsNullOrEmpty(cursorPositionScript))
                    {
                        await webView.ExecuteScriptAsync(cursorPositionScript);
                    }
                }
            }

            // Gemini or Claude
            if (_aiModelType == AIModelType.Gemini || _aiModelType == AIModelType.Claude)
            {
                string editorElementClass = ".ql-editor";
                if (_aiModelType == AIModelType.Claude)
                {
                    editorElementClass = "." + CLAUDE_PROMPT_CLASS;
                }

                if (e.Key == Key.Home || e.Key == Key.End)
                {
                    e.Handled = true; // Prevent default behavior

                    bool shiftPressed = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
                    string cursorPositionScript = $@"
                    (function() {{
                        var editor = document.querySelector('{editorElementClass}');
                        var selection = window.getSelection();
                        if (selection.rangeCount > 0) {{
                            var range = selection.getRangeAt(0);
                            var node = selection.focusNode;
                            var offset = selection.focusOffset;

                            // Normalize node to ensure we're working with text nodes or directly contenteditable
                            while (node && node.nodeType !== 3 && !['DIV', 'P', 'BR'].includes(node.nodeName)) {{
                                node = node.parentNode;
                            }}

                            var textContent = node.nodeType === 3 ? node.data : node.textContent;
                            var position = offset;

                            if ('{e.Key}' === 'Home') {{
                                // Move to the start of the line
                                while (position > 0 && textContent[position - 1] != '\\n') {{
                                    position--;
                                }}
                            }} else if ('{e.Key}' === 'End') {{
                                // Move to the end of the line
                                while (position < textContent.length && textContent[position] != '\\n') {{
                                    position++;
                                }}
                            }}

                            if (node.nodeType === 3) {{
                                if (!{shiftPressed.ToString().ToLower()}) {{
                                    // If Shift is not pressed, move the cursor without selecting
                                    range.setStart(node, position);
                                    range.setEnd(node, position);
                                }} else {{
                                    // If Shift is pressed, adjust the range for selection
                                    if ('{e.Key}' === 'Home') {{
                                        range.setStart(node, position);
                                    }} else if ('{e.Key}' === 'End') {{
                                        range.setEnd(node, position);
                                    }}
                                }}
                            }} else {{
                                // Handling for non-text nodes
                                var child = node.childNodes[0];
                                var childPosition = 0;
                                for (var i = 0; child && i < position; i++) {{
                                    if (child.nodeType === 3) {{
                                        var len = child.data.length;
                                        if (i + len >= position) {{
                                            if (!{shiftPressed.ToString().ToLower()}) {{
                                                range.setStart(child, position - i);
                                                range.setEnd(child, position - i);
                                            }} else {{
                                                if ('{e.Key}' === 'Home') {{
                                                    range.setStart(child, position - i);
                                                }} else if ('{e.Key}' === 'End') {{
                                                    range.setEnd(child, position - i);
                                                }}
                                            }}
                                            break;
                                        }}
                                        i += len;
                                    }} else {{
                                        i++;
                                    }}
                                    child = child.nextSibling;
                                }}
                            }}

                            selection.removeAllRanges();
                            selection.addRange(range);
                        }}
                    }})();";

                    // Execute the JavaScript code
                    if (!string.IsNullOrEmpty(cursorPositionScript))
                    {
                        await webView.CoreWebView2.ExecuteScriptAsync(cursorPositionScript);
                    }
                }
            }
        }

        private async Task AddHandlerCopyCodeAsync()
        {
            try
            {
                // Capture Enter, Home and End keys that should
                // be handled different in this extension to work
                // with Chat GPT/Gemini
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


                string addEventListenersScript = "";

                // Copy code button handler for GPT
                if (_aiModelType == AIModelType.GPT)
                {
                    addEventListenersScript = @"
                        var buttonSelector = '" + GPT_COPY_CODE_BUTTON_SELECTOR + @"';
                        var iconSelector = '" + GPT_COPY_CODE_BUTTON_ICON_SELECTOR + @"';

                        function handleButtonClick(event) {
                            if (!event.target.closest('button').hasAttribute('data-custom-click-handled')) {
                                //event.stopPropagation();
                                window.chrome.webview.postMessage('CopyCodeButtonClicked');
                                event.target.closest('button').setAttribute('data-custom-click-handled', 'true');
                            }
                        }

                        var allButtons = Array.from(document.querySelectorAll(buttonSelector));
                        var allIcons = Array.from(document.querySelectorAll(iconSelector));

                        allButtons.forEach(function(button) {
                            if (!button.hasAttribute('data-listener-added')) {
                                button.addEventListener('click', handleButtonClick, true);
            
                                var buttonText = button.querySelector('svg + *');
                                if (buttonText) {
                                    buttonText.addEventListener('click', handleButtonClick, true);
                                }
            
                                button.setAttribute('data-listener-added', 'true');
                            }
                        });

                        allIcons.forEach(function(icon) {
                            icon.addEventListener('click', handleButtonClick, true);
                        });
                        ";
                }

                // Copy code button handler for Gemini
                if (_aiModelType == AIModelType.Gemini)
                {
                    addEventListenersScript = $@"
                    var allButtons = Array.from(document.getElementsByClassName('{GEMINI_COPY_CODE_BUTTON_CLASS}'));
                    var targetButtons = allButtons.filter(function(button) {{
                        // Check if the button already has the event listener attribute to avoid adding multiple listeners
                        return !button.hasAttribute('data-listener-added');
                    }});

                    targetButtons.forEach(function(button) {{
                        button.addEventListener('click', function() {{
                            // Replace 'CopyCodeButtonClicked' with the appropriate message for your application
                            window.chrome.webview.postMessage('CopyCodeButtonClicked');
                        }});
                        // Mark the button as having the listener added to prevent adding the listener multiple times
                        button.setAttribute('data-listener-added', 'true');
                    }});";
                }

                // Copy code button handler for Claude and new button
                if (_aiModelType == AIModelType.Claude)
                {
                    addEventListenersScript = @"

                    function addClickListener(button, message)
                    {
                        if (!button.hasAttribute('data-listener-added'))
                        {
                            button.addEventListener('click', function() {
                                window.chrome.webview.postMessage(message);
                            });
                            button.setAttribute('data-listener-added', 'true');
                        }
                    }

                    // For existing Claude copy code buttons
                    var allButtons = Array.from(document.querySelectorAll('button'));
                    var copyCodeButtons = allButtons.filter(function(button) {
                        var spanElement = button.querySelector('span');
                        return spanElement && spanElement.textContent.trim() === '" + CLAUDE_COPY_CODE_BUTTON_TEXT + @"';
                    });

                    copyCodeButtons.forEach(function(button) {
                        addClickListener(button, 'CopyCodeButtonClicked');
                    });

                    // For the new Claude artifacts copy code button
                    var newButton = document.querySelector('button.inline-flex[data-state=""closed""][class*=""rounded-md""][class*=""h-8""][class*=""w-8""]');
                    if (newButton)
                    {
                        addClickListener(newButton, 'CopyCodeButtonClicked');
                    }

                    // For the new Claude project copy code button
                    var newButtonSvg = document.querySelector('" + CLAUDE_PROJECT_COPY_CODE_BUTTON_SELECTOR + @"');
                    if (newButtonSvg)
                    {
                        var newButton = newButtonSvg.closest('button');
                        if (newButton)
                        {
                            addClickListener(newButton, 'CopyCodeButtonClicked');
                        }
                    }
                    ";
                }

                await webView.ExecuteScriptAsync(addEventListenersScript);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error in AddHanlderCopyCode(): " + ex.Message);
            }
        }

        private async Task SubmitPromptAIAsync()
        {
            if (_aiModelType == AIModelType.GPT)
            {
                // Submit the GPT prompt
                string script1 = "document.querySelector('button[data-testid=\"fruitjuice-send-button\"]').click();";
                await webView.ExecuteScriptAsync(script1);

                // Wait a bit
                await Task.Delay(3000);

                // Click on the scroll to bottom button
                string initialScript = @"var button = document.querySelector('button.absolute[class*=""bottom-5""] svg.icon-md');
                                            if (button) {
                                                button.parentElement.click();
                                            }";

                await webView.ExecuteScriptAsync(initialScript);

                // Wait a bit
                await Task.Delay(2000);

                // Click in the scroll to bottom button
                await webView.ExecuteScriptAsync(initialScript);
            }

            if (_aiModelType == AIModelType.Gemini)
            {
                // Submit the Gemini prompt
                string script2 = @"var icons = document.querySelectorAll('mat-icon');
                                    for (var i = 0; i < icons.length; i++) {
                                      if (icons[i].textContent.trim().toLowerCase() === 'send') {
                                        icons[i].click();
                                        break; // Stop the loop once the correct icon is clicked
                                      }
                                    }";
                await webView.ExecuteScriptAsync(script2);
            }

            if (_aiModelType == AIModelType.Claude)
            {
                // Wait a bit
                await Task.Delay(800);

                // Submit the Claude prompt (we have different selector for first chat message and other chat messages)
                string script4 = "document.querySelector('[aria-label=\"Send Message\"]').click();";
                await webView.ExecuteScriptAsync(script4);

                // Click on button with 
                // Submit the Claude prompt
                string script3 = "document.querySelector('button.w-full.flex.items-center.bg-bg-200').click();";
                await webView.ExecuteScriptAsync(script3);
            }
        }

        private async Task<bool> IsFileAttachedAsync()
        {
            if (_aiModelType == AIModelType.GPT)
            {
                // Check if we have an attachment from this extension
                string script = @"
            var result = 'notfound';
            var divs = document.querySelectorAll('div.truncate.font-semibold');
            for (var i = 0; i < divs.length; i++) {
                if (divs[i].textContent.startsWith('GPTExtension_')) {
                    result = 'found';
                    break;
                }
            }
            result;";

                try
                {
                    string result = await webView.CoreWebView2.ExecuteScriptAsync(script);
                    return result == "\"found\"";
                }
                catch (Exception)
                {
                    return false;
                }
            }

            if (_aiModelType == AIModelType.Claude)
            {
                // Check if we have an attachment from this extension
                string script = @"
            var result = 'notfound';
            var divs = document.querySelectorAll('div[data-testid]');
            for (var i = 0; i < divs.length; i++) {
                if (divs[i].getAttribute('data-testid').startsWith('GPTExtension_')) {
                    result = 'found';
                    break;
                }
            }
            result;";

                try
                {
                    string result = await webView.CoreWebView2.ExecuteScriptAsync(script);
                    return result == "\"found\"";
                }
                catch (Exception)
                {
                    return false;
                }
            }

            if (_aiModelType == AIModelType.Gemini)
            {
                return false;
            }

            return false;
        }

        private bool _isCreatingNewFile = false;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "<Pending>")]
        private async void WebView_WebMessageReceived(object sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // When Copy code button was clicked in AI app, automatically insert the code in VS.NET
                if (e.TryGetWebMessageAsString() == "CopyCodeButtonClicked" && _enableCopyCode && !_isCreatingNewFile)
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
                        await SubmitPromptAIAsync();
                        break;
                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error in WebView_WebMessageReceived(): " + ex.Message);
            }
        }

        private async Task CreateNewFileFromAIAsync(string textFromBrowser)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                string codeFromAI = textFromBrowser;

                if (!string.IsNullOrEmpty(codeFromAI))
                {
                    DTE2 dte = (DTE2)Package.GetGlobalService(typeof(DTE));
                    Solution solution = dte.Solution;
                    Project activeProject = solution.Projects.OfType<Project>().FirstOrDefault();

                    if (activeProject == null)
                    {
                        MessageBox.Show("No active project found.", "Unsupported Operation", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    string fileExtension = GetFileExtensionForProject(activeProject);
                    string fileName = PromptForFileName(fileExtension);
                    if (string.IsNullOrEmpty(fileName))
                    {
                        return;
                    }

                    await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                        try
                        {
                            string projectDir = Path.GetDirectoryName(activeProject.FullName);
                            string filePath = Path.Combine(projectDir, fileName);

                            // Add the new file to the project directory
                            File.WriteAllText(filePath, codeFromAI);

                            // Add the new file to the project
                            ProjectItem newFile = activeProject.ProjectItems.AddFromFile(filePath);

                            // Open the new file
                            EnvDTE.Window window = newFile.Open(EnvDTE.Constants.vsViewKindTextView);
                            window.Visible = true;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error creating file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    });

                    // Format the code
                    FormatCodeInVS();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Change the event handler signature
        private async Task OnNewFileButtonClickAsync(object sender, RoutedEventArgs e)
        {
            // Retrieve selected code in GPT, insert and format in VS.NET
            string textFromBrowser = await GetSelectedTextFromAIAsync();

            // If we have text selected in browser send to VS.NET
            if (!string.IsNullOrEmpty(textFromBrowser))
            {
                await CreateNewFileFromAIAsync(textFromBrowser);
            }
            else
            {
                MessageBox.Show("Please select code in the AI page to create a new file!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private string GetFileExtensionForProject(Project project)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            string projectKind = project.Kind;

            switch (projectKind)
            {
                case "{F184B08F-C81C-45F6-A57F-5ABD9991F28F}":
                    return ".vb";
                case "{E6FDF86B-F3D1-11D4-8576-0002A516ECE8}":
                    return ".js";
                case "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}":
                    return ".cpp";
                case "{A1591282-1198-4647-A2B1-27E5FF5F6F3B}": // TypeScript Project
                    return ".ts";
                case "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}": // C# Project
                    return ".cs";
                default:
                    return ".txt"; // Default to .txt if project type is unknown
            }
        }

        private string PromptForFileName(string defaultExtension)
        {
            var inputDialog = new InputDialog("New File", "Enter new file name", $"NewFile{defaultExtension}");
            if (inputDialog.ShowDialog() == true)
            {
                string fileName = inputDialog.ResponseText.Trim();

                if (!fileName.EndsWith(defaultExtension, StringComparison.OrdinalIgnoreCase))
                {
                    fileName += defaultExtension;
                }

                if (fileName.StartsWith("."))
                    return "";

                return fileName;
            }
            return null;
        }

        private System.Timers.Timer timer;
        private JoinableTaskFactory _joinableTaskFactory;

        private async Task StartTimerAsync()
        {
            try
            {
                await _joinableTaskFactory.SwitchToMainThreadAsync();

                StopTimer();

                // Timer to add handler for GPT copy code click
                timer = new System.Timers.Timer(2000);
                timer.Elapsed += HandleTimerElapsed;
                timer.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error in StartTimerAsync(): " + ex.Message);
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

        private async Task CheckTimerStatusAsync()
        {
            while (true)
            {
                await Task.Delay(5000); // Check every 5 seconds

                if (timer == null || !timer.Enabled)
                {
                    Debug.WriteLine("Timer is not running. Restarting the timer.");
                    await StartTimerAsync();
                }
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

        #region Other Actions for GPT/Gemini to process and Reload GPT/Gemini
#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void OnCompleteCodeButtonClick(object sender, RoutedEventArgs e)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            try
            {
                // Define the promptCompleteCode prompt
                string promptCompleteCode = "Please show new full complete code without explanations with complete methods implementation for the provided code without any placeholders like ... or assuming code segments. Do not create methods you dont know. Keep all original comments.";

                string script = string.Empty;

                if (_aiModelType == AIModelType.Claude)
                {
                    // Set the innerText to set the AI prompt in Claude
                    script = $@"
                            var element = document.querySelector('.{CLAUDE_PROMPT_CLASS}');
                                element.innerText = '{promptCompleteCode}';

                                var inputEvent = new Event('input', {{
                                    'bubbles': true,
                                    'cancelable': true
                                }});
                             element.dispatchEvent(inputEvent); ";
                }

                if (_aiModelType == AIModelType.Gemini)
                {
                    // Set the innerText to set the AI prompt in Gemini
                    script = GetScriptGeminiReceiveCode(promptCompleteCode);
                }

                if (_aiModelType == AIModelType.GPT)
                {
                    // Set the AI prompt in GPT
                    script = $@"
                            document.getElementById('{GPT_PROMPT_TEXT_AREA_ID}').value = '{promptCompleteCode}';
                
                            var inputEvent = new Event('input', {{
                                'bubbles': true,
                                'cancelable': true
                            }});
                            document.getElementById('{GPT_PROMPT_TEXT_AREA_ID}').dispatchEvent(inputEvent);";
                }

                // Execute the constructed script in the WebView context
                await webView.CoreWebView2.ExecuteScriptAsync(script);

                // Also submit the prompt, assuming SubmitPromptGPTAsync() is a method to submit the prompt in GPT
                await SubmitPromptAIAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnCompleteCodeButtonClick(): {ex.Message}");
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "<Pending>")]
        private async void OnSendCodeMenuItemClick(object sender, RoutedEventArgs e)
        {
            try
            {
                // Process other actions for the AI
                if (sender is MenuItem menuItem && menuItem.Tag is string command)
                {
                    await SendSelectedCodeToAIAsync(command);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error in OnSendCodeMenuItemClick(): " + ex.Message);
            }
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void OnReloadAIItemClick(object sender, RoutedEventArgs e)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            try
            {
                await _joinableTaskFactory.SwitchToMainThreadAsync();

                if (_aiModelType == AIModelType.GPT)
                {
                    webView.Source = new Uri(CHAT_GPT_URL);
                    await WaitForElementByIdAsync(GPT_PROMPT_TEXT_AREA_ID);
                }
                if (_aiModelType == AIModelType.Gemini)
                {
                    webView.Source = new Uri(GEMINI_URL);
                    await WaitForElementByClassAsync(GEMINI_PROMPT_CLASS);
                }
                if (_aiModelType == AIModelType.Claude)
                {
                    webView.Source = new Uri(CLAUDE_URL);
                    await WaitForElementByClassAsync(CLAUDE_PROMPT_CLASS);
                }

                await StartTimerAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnReloadAIItemClick(): {ex.Message}");
            }
        }

        #endregion

        #region Find out language of the active document

        private string GetActiveFileLanguage()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_serviceProvider == null)
                return string.Empty;

            // Get the active document
            DTE dte = (DTE)_serviceProvider.GetService(typeof(DTE));
            if (dte != null && dte.ActiveDocument != null)
            {
                // Get the file extension
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

                // Check if the file extension is supported and return name
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
            reloadMenuItem.Click += OnReloadAIItemClick;
            CodeActionsContextMenu.Items.Add(reloadMenuItem);

            // Add Configure extension... menu item
            var configureMenuItem = new MenuItem { Header = "Configure extension..." };
            configureMenuItem.Click += ConfigureExtensionMenuItem_Click;
            CodeActionsContextMenu.Items.Add(configureMenuItem);

            // Add another separator before the new options
            CodeActionsContextMenu.Items.Add(new Separator());

            var useGeminiMenuItem = new MenuItem { Header = "Use Gemini", IsCheckable = true };
            var useGptMenuItem = new MenuItem { Header = "Use GPT", IsCheckable = true };
            var useClaudeMenuItem = new MenuItem { Header = "Use Claude", IsCheckable = true };

            // Configure "Use GPT" menu item
            useGptMenuItem.Click += (sender, e) =>
            {
                _aiModelType = AIModelType.GPT;
                useGptMenuItem.IsChecked = true;
                useGeminiMenuItem.IsChecked = false;
                useClaudeMenuItem.IsChecked = false;
                reloadMenuItem.Header = "Reload Chat GPT...";
                _parentToolWindow.Caption = "Chat GPT Extension";
                UpdateButtonContentAndTooltip();
                OnReloadAIItemClick(null, null);
                SaveConfiguration();
            };
            CodeActionsContextMenu.Items.Add(useGptMenuItem);

            // Configure "Use Gemini" menu item
            useGeminiMenuItem.Click += (sender, e) =>
            {
                _aiModelType = AIModelType.Gemini;
                useGeminiMenuItem.IsChecked = true;
                useGptMenuItem.IsChecked = false;
                useClaudeMenuItem.IsChecked = false;
                reloadMenuItem.Header = "Reload Gemini...";
                _parentToolWindow.Caption = "Gemini Extension";
                UpdateButtonContentAndTooltip();
                OnReloadAIItemClick(null, null);
                SaveConfiguration();
            };
            CodeActionsContextMenu.Items.Add(useGeminiMenuItem);

            // Configure "Use Claude" menu item
            useClaudeMenuItem.Click += (sender, e) =>
            {
                _aiModelType = AIModelType.Claude;
                useGeminiMenuItem.IsChecked = false;
                useGptMenuItem.IsChecked = false;
                useClaudeMenuItem.IsChecked = true;
                reloadMenuItem.Header = "Reload Claude...";
                _parentToolWindow.Caption = "Claude Extension";
                UpdateButtonContentAndTooltip();
                OnReloadAIItemClick(null, null);
                SaveConfiguration();
            };
            CodeActionsContextMenu.Items.Add(useClaudeMenuItem);

            // Set the initial state based on _gptConfigured
            useGptMenuItem.IsChecked = (_aiModelType == AIModelType.GPT);
            useGeminiMenuItem.IsChecked = (_aiModelType == AIModelType.Gemini);
            useClaudeMenuItem.IsChecked = (_aiModelType == AIModelType.Claude);

            // Set all the user interface according to the AI model selected
            if (_aiModelType == AIModelType.GPT)
            {
                reloadMenuItem.Header = "Reload Chat GPT...";
                _parentToolWindow.Caption = "Chat GPT Extension";
                UpdateButtonContentAndTooltip();
            }
            if (_aiModelType == AIModelType.Gemini)
            {
                reloadMenuItem.Header = "Reload Gemini...";
                _parentToolWindow.Caption = "Gemini Extension";
                UpdateButtonContentAndTooltip();
            }
            if (_aiModelType == AIModelType.Claude)
            {
                reloadMenuItem.Header = "Reload Claude...";
                _parentToolWindow.Caption = "Claude Extension";
                UpdateButtonContentAndTooltip();
            }

            StopTimer();
        }

        private void UpdateButtonContentAndTooltip()
        {
            // Determine the AI technology based on the AI model selected
            string aiTechnology = _aiModelType.ToString();

            // Update the content and tooltip for the buttons
            btnVSNETToAI.Content = $"VS.NET to {aiTechnology} ➡️";
            btnVSNETToAI.ToolTip = $"Transfer selected code from VS.NET to {aiTechnology}";

            btnFixCodeInAI.Content = $"Fix Code in {aiTechnology} ➡️";
            btnFixCodeInAI.ToolTip = $"Fix bugs in VS.NET selected code using {aiTechnology}";

            btnImproveCodeInAI.Content = $"Improve Code in {aiTechnology} ➡️";
            btnImproveCodeInAI.ToolTip = $"Refactor selected code from VS.NET in {aiTechnology}";


            btnAIToVSNET.Content = $"⬅️ {aiTechnology} to VS.NET";
            btnAIToVSNET.ToolTip = $"Transfer selected code from {aiTechnology} to VS.NET";

            btnAttachFile.Content = $"Attach Open File to {aiTechnology}📎";
            btnAttachFile.ToolTip = $"Attach VS.NET file open to {aiTechnology}";

            btnCompleteCodeInAI.Content = $"Complete Code in {aiTechnology} ✅";
            btnCompleteCodeInAI.ToolTip = $"Ask {aiTechnology} to generate complete code";


            btnNewFile.ToolTip = $"Select code in {aiTechnology} to create a new file in VS.NET";

            btnContinueCode.Content = $"Continue Code in {aiTechnology} ⏩";
            btnContinueCode.ToolTip = $"Ask {aiTechnology} to continue the code generation";

            EnableCopyCodeCheckBox.ToolTip = $"Enable sending code from {aiTechnology} to VS.NET when Copy code button is clicked in {aiTechnology}";
        }

        #endregion

        #region Load/Save Extension Configuration

        private const string _configurationFileName = "configuration.json";
        private static readonly string _appDataPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChatGPTExtension", "Actions");
        private static readonly string _fullConfigPath = System.IO.Path.Combine(_appDataPath, _configurationFileName);

        public void SaveConfiguration()
        {
            var configuration = new Configuration { GptConfigured = (int)_aiModelType };

            // Ensure the directory exists
            Directory.CreateDirectory(_appDataPath);

            // Serialize and save the configuration to a file
            var json = JsonConvert.SerializeObject(configuration, Formatting.Indented);
            File.WriteAllText(_fullConfigPath, json);
        }

        public AIModelType LoadConfiguration()
        {
            try
            {
                // Check if the configuration file exists
                if (File.Exists(_fullConfigPath))
                {
                    // Read and deserialize the configuration from the file
                    var json = File.ReadAllText(_fullConfigPath);
                    var configuration = JsonConvert.DeserializeObject<Configuration>(json);

                    return (AIModelType)configuration?.GptConfigured;
                }
            }
            catch
            {
            }

            return AIModelType.GPT; // Default to GPT if the file does not exist or any error
        }

        #endregion

        #region Attach Current File Button

#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void OnAttachFileButtonClick(object sender, RoutedEventArgs e)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // If current model is Gemini say it is not supported and return
                if (_aiModelType == AIModelType.Gemini)
                {
                    // Display a message that the operation is not supported in Gemini
                    MessageBox.Show("Attaching code files is currently not supported in Gemini.", "Unsupported Operation", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Get the active document in VS.NET
                DTE2 dte = (DTE2)Package.GetGlobalService(typeof(DTE));
                if (dte?.ActiveDocument != null)
                {
                    try
                    {
                        var textDocument = dte.ActiveDocument.Object("TextDocument") as TextDocument;
                        if (textDocument != null)
                        {
                            var editPoint = textDocument.StartPoint.CreateEditPoint();
                            if (editPoint != null)
                            {
                                var fileContent = editPoint.GetText(textDocument.EndPoint);

                                // Generate a file name in the user's directory
                                string userDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                                string fileName = "GPTExtension_" + Guid.NewGuid().ToString() + ".txt";
                                string filePath = Path.Combine(userDirectory, fileName);

                                // Write the file content to the temporary file
                                File.WriteAllText(filePath, fileContent);

                                // Copy the file path to the clipboard
                                Clipboard.SetText(filePath);

                                if (_aiModelType == AIModelType.Claude)
                                {
                                    // Simulate click on the file input element using JavaScript
                                    string clickFileInputScript = @"document.querySelector('input[data-testid=""file-upload""]')?.click();";
                                    await webView.CoreWebView2.ExecuteScriptAsync(clickFileInputScript);
                                }

                                // If the AI model type is GPT
                                if (_aiModelType == AIModelType.GPT)
                                {
                                    // Simulate clicking the attach file button for GPT
                                    string clickButtonScript = @"document.querySelector('button.juice\\:w-8')?.click();";
                                    await webView.CoreWebView2.ExecuteScriptAsync(clickButtonScript);

                                    // Simulate clicking the last menu item
                                    string clickMenuItemScript = @"setTimeout(() => {
                                                        const menuItems = document.querySelectorAll('div[role=""menuitem""]');
                                                        if (menuItems?.length > 0) {
                                                            menuItems[menuItems.length - 1]?.click();
                                                        }
                                                    }, 800); // Delay to allow menu to appear after button click
                                               ";
                                    await webView.CoreWebView2.ExecuteScriptAsync(clickMenuItemScript);
                                }

                                // Wait for the file dialog to open
                                await Task.Delay(1500); // Adjust the delay as necessary

                                // Simulate pasting the file path and pressing enter
                                System.Windows.Forms.SendKeys.SendWait("^v");  // Paste the file path

                            }
                        }
                    }
                    catch (COMException ex)
                    {
                        Debug.WriteLine($"Error accessing active document: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnAttachFileButtonClick(): {ex.Message}");
            }
        }

        #endregion

        #region New File Button/Continue Code Button

#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void OnNewFileButtonClick(object sender, RoutedEventArgs e)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            try
            {
                // Retrieve selected code in GPT, insert and format in VS.NET
                string textFromBrowser = await GetSelectedTextFromAIAsync();

                // If we have text selected in browser send to VS.NET
                if (!string.IsNullOrEmpty(textFromBrowser))
                {
                    await CreateNewFileFromAIAsync(textFromBrowser);
                }
                else
                {
                    MessageBox.Show("Please select code in the AI page to create a new file!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch { }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "<Pending>")]
        private async void OnContinueCodeButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                string promptContinueCode = "Continue code generation";

                string script = string.Empty;

                if (_aiModelType == AIModelType.Claude)
                {
                    script = $@"
                        var element = document.querySelector('.{CLAUDE_PROMPT_CLASS}');
                        element.innerText = '{promptContinueCode}';

                        var inputEvent = new Event('input', {{
                            'bubbles': true,
                            'cancelable': true
                        }});
                        element.dispatchEvent(inputEvent);";
                }
                else if (_aiModelType == AIModelType.Gemini)
                {
                    script = GetScriptGeminiReceiveCode(promptContinueCode);
                }
                else if (_aiModelType == AIModelType.GPT)
                {
                    script = $@"
                        document.getElementById('{GPT_PROMPT_TEXT_AREA_ID}').value = '{promptContinueCode}';
        
                        var inputEvent = new Event('input', {{
                            'bubbles': true,
                            'cancelable': true
                        }});
                        document.getElementById('{GPT_PROMPT_TEXT_AREA_ID}').dispatchEvent(inputEvent);";
                }

                await webView.CoreWebView2.ExecuteScriptAsync(script);
                await SubmitPromptAIAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnContinueCodeButtonClick(): {ex.Message}");
            }
        }

        #endregion

        #region Minimize/Restore window when undocked

        private Size _originalSize;
        private Point _originalPosition;
        private Point _minimizedPosition;
        private bool _isMinimized = false;
        private const double MINIMIZE_WIDTH = 160;
        private const double MINIMIZE_HEIGHT = 30;
        private MenuItem _minimizeRestoreMenuItem;
        private WindowState _previousWindowState;
        private DispatcherTimer _updateTimer;
        private WindowHelper _windowHelper;

        /// <summary>
        /// Adds the Minimize/Restore menu item to the context menu.
        /// </summary>
        private void AddMinimizeRestoreMenuItem()
        {
            _minimizeRestoreMenuItem = new MenuItem { Header = "Minimize" };
            _minimizeRestoreMenuItem.Click += MinimizeRestoreMenuItem_Click;
            CodeActionsContextMenu.Items.Insert(0, _minimizeRestoreMenuItem);
            CodeActionsContextMenu.Items.Insert(1, new Separator());
        }

        /// <summary>
        /// Timer tick event handler to update menu visibility.
        /// </summary>
        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            UpdateMinimizeRestoreMenuVisibility();
        }

        /// <summary>
        /// Updates the visibility of the Minimize/Restore menu item based on window state.
        /// </summary>
        private void UpdateMinimizeRestoreMenuVisibility()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var vsWindow = GetVsWindow();
            bool isFloating = vsWindow != null && _windowHelper.IsWindowFloating(vsWindow);
            _minimizeRestoreMenuItem.Visibility = isFloating ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Gets the Visual Studio Window object for the current tool window.
        /// </summary>
        /// <returns>The EnvDTE.Window object or null if not found.</returns>
        private EnvDTE.Window GetVsWindow()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_parentToolWindow?.Frame is IVsWindowFrame frame)
            {
                if (frame.GetProperty((int)__VSFPROPID.VSFPROPID_ExtWindowObject, out object windowObject) == Microsoft.VisualStudio.VSConstants.S_OK)
                {
                    return windowObject as EnvDTE.Window;
                }
            }
            return null;
        }

        /// <summary>
        /// Event handler for the Minimize/Restore menu item click.
        /// </summary>
        private void MinimizeRestoreMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_isMinimized)
            {
                RestoreWindow();
            }
            else
            {
                MinimizeWindow();
            }
        }

        /// <summary>
        /// Minimizes the window to a small size and moves it to the minimized position.
        /// </summary>
        private void MinimizeWindow()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var vsWindow = GetVsWindow();
            if (vsWindow != null && _windowHelper.IsWindowFloating(vsWindow))
            {
                var wpfWindow = Window.GetWindow(this);
                if (wpfWindow != null)
                {
                    // Save original size, position, and state
                    _originalSize = new Size(wpfWindow.ActualWidth, wpfWindow.ActualHeight);
                    _originalPosition = new Point(wpfWindow.Left, wpfWindow.Top);
                    _previousWindowState = wpfWindow.WindowState;

                    // Set new size and position
                    wpfWindow.WindowState = WindowState.Normal;
                    wpfWindow.Width = MINIMIZE_WIDTH;
                    wpfWindow.Height = MINIMIZE_HEIGHT;

                    // If it's the first time minimizing, set the minimized position
                    if (_minimizedPosition == default(Point))
                    {
                        _minimizedPosition = new Point(200, 200);
                    }

                    wpfWindow.Left = _minimizedPosition.X;
                    wpfWindow.Top = _minimizedPosition.Y;

                    // Update UI
                    _isMinimized = true;
                    btnVSNETToAI.Content = "Restore";
                    btnVSNETToAI.ToolTip = "Restore the floating window to its original size";
                    btnFixCodeInAI.Visibility = Visibility.Hidden;
                    btnImproveCodeInAI.Visibility = Visibility.Hidden;
                    UpdateMinimizeRestoreMenuItemHeader();
                }
            }
        }

        /// <summary>
        /// Restores the window to its original size and position.
        /// </summary>
        private void RestoreWindow()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var vsWindow = GetVsWindow();
            if (vsWindow != null && _windowHelper.IsWindowFloating(vsWindow))
            {
                var wpfWindow = Window.GetWindow(this);
                if (wpfWindow != null)
                {
                    // Save the current minimized position
                    _minimizedPosition = new Point(wpfWindow.Left, wpfWindow.Top);

                    // Restore original size, position, and state
                    wpfWindow.Width = _originalSize.Width;
                    wpfWindow.Height = _originalSize.Height;
                    wpfWindow.Left = _originalPosition.X;
                    wpfWindow.Top = _originalPosition.Y;
                    wpfWindow.WindowState = _previousWindowState;

                    // Update UI
                    _isMinimized = false;
                    btnFixCodeInAI.Visibility = Visibility.Visible;
                    btnImproveCodeInAI.Visibility = Visibility.Visible;
                    UpdateButtonContentAndTooltip();
                    UpdateMinimizeRestoreMenuItemHeader();
                }
            }
        }

        /// <summary>
        /// Updates the header of the Minimize/Restore menu item.
        /// </summary>
        private void UpdateMinimizeRestoreMenuItemHeader()
        {
            _minimizeRestoreMenuItem.Header = _isMinimized ? "Restore" : "Minimize";
        }

        #endregion
    }

    public class WindowHelper
    {
        private DTE2 _dte;

        public WindowHelper(DTE2 dte)
        {
            _dte = dte;
        }

        public bool IsWindowFloating(EnvDTE.Window window)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            return window.IsFloating;
        }
    }
}
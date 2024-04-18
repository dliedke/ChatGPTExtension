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

using EnvDTE;
using EnvDTE80;
using Newtonsoft.Json;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;
using Microsoft.VisualStudio.Shell;

namespace ChatGPTExtension
{
    public partial class GptToolWindowControl : UserControl
    {
        #region Web IDs and URLs for GPT and Gemini

        // Note: There are ids also in method SubmitPromptAIAsync()

        // IDs and selectors might need updates in new GPT versions
        private const string CHAT_GPT_URL = "https://chat.openai.com/";
        private const string GPT_PROMPT_TEXT_AREA_ID = "prompt-textarea";
        private const string GPT_COPY_CODE_BUTTON_TEXT = "Copy code";

        // Selectors might need updates in new Gemini versions
        private const string GEMINI_URL = "https://gemini.google.com";
        private const string GEMINI_PROMPT_CLASS = "ql-editor";
        private const string GEMINI_COPY_CODE_BUTTON_CLASS = "copy-button";

        // Selectors might need updates in new Claude versions
        private const string CLAUDE_URL = "https://claude.ai/chats";
        private const string CLAUDE_PROMPT_CLASS = "ProseMirror";
        private const string CLAUDE_COPY_CODE_BUTTON_TEXT = "Copy code";

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

            InitializeComponent();

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            InitializeAsync();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            _aiModelType = LoadConfiguration();
            LoadContextMenuActions();
            _parentToolWindow = parent;

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
                Button btn = sender as Button;
                string extraCommand = btn.Tag as string;

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
                    script = $@"var element = document.querySelector('.{GEMINI_PROMPT_CLASS}');
                                element.innerText = {JsonConvert.SerializeObject(selectedCode)};
               
                                var inputEvent = new Event('input', {{
                                    'bubbles': true,
                                    'cancelable': true
                                }});
                                element.dispatchEvent(inputEvent);";
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
                    script = $@"var existingText = document.querySelector('.{GEMINI_PROMPT_CLASS}').innerText;
                       document.querySelector('.{GEMINI_PROMPT_CLASS}').innerText = existingText + '\r\n' + {JsonConvert.SerializeObject(selectedCode)};
               
                       var inputEvent = new Event('input', {{
                           'bubbles': true,
                           'cancelable': true
                       }});
                       document.querySelector('.{GEMINI_PROMPT_CLASS}').dispatchEvent(inputEvent);";
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
                    addEventListenersScript = $@"
                        var allButtons = Array.from(document.querySelectorAll('button'));
                        var targetButtons = allButtons.filter(function(button) {{
                            return button.textContent.trim() === '{GPT_COPY_CODE_BUTTON_TEXT}' && !button.hasAttribute('data-listener-added');
                        }});

                        targetButtons.forEach(function(button) {{
                            button.addEventListener('click', function() {{
                                window.chrome.webview.postMessage('CopyCodeButtonClicked');
                            }});
                            // Mark the button as having the listener added
                            button.setAttribute('data-listener-added', 'true');
                        }});
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

                // Copy code button handler for Claude
                if (_aiModelType == AIModelType.Claude)
                {
                    addEventListenersScript = $@"
                    var allButtons = Array.from(document.querySelectorAll('button'));
                    var targetButtons = allButtons.filter(function(button) {{
                        var spanElement = button.querySelector('span');
                        return spanElement && spanElement.textContent.trim() === '{CLAUDE_COPY_CODE_BUTTON_TEXT}' && !button.hasAttribute('data-listener-added');
                    }});

                    targetButtons.forEach(function(button) {{
                        button.addEventListener('click', function() {{
                            window.chrome.webview.postMessage('CopyCodeButtonClicked');
                        }});
                        // Mark the button as having the listener added
                        button.setAttribute('data-listener-added', 'true');
                    }});";
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
                string script1 = "document.querySelector('[data-testid=\"send-button\"]').click();";
                await webView.ExecuteScriptAsync(script1);

                // Wait a bit
                await Task.Delay(3000);

                // Click in the scroll to bottom button
                string initialScript = "var button = document.querySelector('button.cursor-pointer.absolute'); if (button) { button.click(); }";
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
                // Submit the Claude prompt (we have different selector for first chat message and other chat messages)
                string script4 = "document.querySelector('[aria-label=\"Send Message\"]').click();";
                await webView.ExecuteScriptAsync(script4);

                // Submit the Claude prompt
                string script3 = "document.querySelector('button.w-full.flex.items-center.bg-bg-200').click();";
                await webView.ExecuteScriptAsync(script3);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "<Pending>")]
        private async void WebView_WebMessageReceived(object sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // When Copy code button was clicked in AI app, automatically insert the code in VS.NET
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
                        await SubmitPromptAIAsync();
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

                // Timer to add handler for GPT copy code click
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

        #region Other Actions for GPT/Gemini to process and Reload GPT/Gemini
#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void OnCompleteCodeButtonClick(object sender, RoutedEventArgs e)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            try
            {
                // Define the promptCompleteCode prompt
                string promptCompleteCode = "Please show new full complete code without explanations with complete methods implementation for the provided code without any placeholders like ... or assuming code segments. Do not create methods you dont know.";

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
                    script = $@"
                            var element = document.querySelector('.{GEMINI_PROMPT_CLASS}');
                                element.innerText = '{promptCompleteCode}';

                                var inputEvent = new Event('input', {{
                                    'bubbles': true,
                                    'cancelable': true
                                }});
                             element.dispatchEvent(inputEvent); ";
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnReloadChatGptItemClick(): {ex.Message}");
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
            btnFixCodeInAI.ToolTip = $"Transfer selected code from {aiTechnology} to VS.NET";

            btnImproveCodeInAI.Content = $"Improve Code in {aiTechnology} ➡️";
            btnImproveCodeInAI.ToolTip = $"Refactor selected code from VS.NET in {aiTechnology}";

            btnAIToVSNET.Content = $"⬅️ {aiTechnology} to VS.NET";
            btnAIToVSNET.ToolTip = $"Transfer selected code from {aiTechnology} to VS.NET";
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

    }
}
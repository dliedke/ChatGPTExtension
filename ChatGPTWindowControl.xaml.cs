﻿/* *******************************************************************************************************************
 * Application: ChatGPTExtension
 * 
 * Autor:  Daniel Liedke
 * 
 * Copyright © Daniel Liedke 2025
 * Usage and reproduction in any manner whatsoever without the written permission of Daniel Liedke is strictly forbidden.
 *  
 * Purpose: Main user control for the Chat GPT/Gemini/Claude extension for VS.NET 2022
 *           
 * *******************************************************************************************************************/

using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Window = System.Windows.Window;

namespace ChatGPTExtension
{
    public partial class GptToolWindowControl : UserControl
    {
        #region Constructor and Initialization

        private DTE2 _dte;
        private Events _events;
        private WindowEvents _windowEvents;
        private bool _enableCopyCode = true;
        private readonly IServiceProvider _serviceProvider;
        private ConfigurationWindow _configWindow = new ConfigurationWindow();
        private ButtonLabelsConfiguration _buttonLabels = ButtonLabelsConfiguration.Load();
        private AIModelType _aiModelType = AIModelType.GPT;
        private ChatGPTToolWindow _parentToolWindow;

        public enum AIModelType : int
        {
            GPT = 1,
            Gemini = 2,
            Claude = 3,
            DeepSeek = 4
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD110:Observe result of async calls", Justification = "<Pending>")]
        public GptToolWindowControl(IServiceProvider serviceProvider, ChatGPTToolWindow parent)
        {
            _parentToolWindow = parent;
            _serviceProvider = serviceProvider;

            // Initialize the JoinableTaskFactory
            _joinableTaskFactory = ThreadHelper.JoinableTaskFactory;

            InitializeComponent();

            // Initialize WindowHelper
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = serviceProvider.GetService(typeof(DTE)) as DTE2;
            _windowHelper = new WindowHelper(dte);
        }

        bool _initialized = false;

        private void ChatGPTWindowControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_initialized)
                return;

            if (this.IsVisible)
            {
                // Initialize the JoinableTaskFactory
                _joinableTaskFactory = ThreadHelper.JoinableTaskFactory;

                // Run sync InitializeAsync() using _joinableTaskFactory
                _joinableTaskFactory.RunAsync(async () =>
                {
                    await InitializeAsync();
                }).FileAndForget("InitializeAsync");

                _aiModelType = LoadConfiguration();

                LoadContextMenuActions();
                LoadComboBoxItemsKi();

                AddMinimizeRestoreMenuItem();

                // Set up a timer to periodically check the window state
                _updateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _updateTimer.Tick += UpdateTimer_Tick;
                _updateTimer.Start();

                _initialized = true; // Mark as initialized
            }
        }

        public async Task InitializeConfigurationAsync()
        {
            try
            {
                await AIConfiguration.AIConfigurationManager.Instance.InitializeAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing configuration: {ex.Message}");
            }
        }

        private bool _isWebViewInitialized = false;
        private readonly object _initializationLock = new object();

        private async Task InitializeAsync()
        {
            try
            {
                // Usa lock para evitar múltiplas inicializações simultâneas
                lock (_initializationLock)
                {
                    if (_isWebViewInitialized || webView.CoreWebView2 != null)
                    {
                        Debug.WriteLine("WebView2 already initialized, skipping initialization");
                        return;
                    }
                    _isWebViewInitialized = true; // Marca imediatamente para evitar reentrada
                }

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Initialize the configuration first
                await InitializeConfigurationAsync();

                _dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
                if (_dte != null)
                {
                    _events = _dte.Events;
                    _windowEvents = _events.WindowEvents;
                }

                // Check if the process was started with /rootsuffix Exp to determine if it's a debug instance
                bool isDebugInstance = Environment.GetCommandLineArgs().Any(arg =>
                    arg.ToLowerInvariant().Contains("/rootsuffix") ||
                    arg.ToLowerInvariant().Contains("exp"));

                // Add debug suffix to the user data folder if in debug mode
                string folderSuffix = isDebugInstance ? "_Debug" : "";

                string userDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ChatGPTExtension",
                    $"WebView2{folderSuffix}");

                if (!Directory.Exists(userDataPath))
                {
                    Directory.CreateDirectory(userDataPath);
                }

                string edgeWebView2Path = GetEdgeWebView2Path();
                Debug.WriteLine($"EdgeWebView2Path: {edgeWebView2Path ?? "null"}");
                Debug.WriteLine($"UserDataPath: {userDataPath}");

                CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: edgeWebView2Path,
                    userDataFolder: userDataPath);

                await webView.EnsureCoreWebView2Async(environment);

                switch (_aiModelType)
                {
                    case AIModelType.GPT:
                        webView.Source = new Uri(AIConfiguration.GPTUrl);
                        await WaitForElementByIdAsync(AIConfiguration.GPTPromptTextAreaId);
                        break;
                    case AIModelType.Gemini:
                        webView.Source = new Uri(AIConfiguration.GeminiUrl);
                        await WaitForElementByClassAsync(AIConfiguration.GeminiPromptClass);
                        break;
                    case AIModelType.Claude:
                        webView.Source = new Uri(AIConfiguration.ClaudeUrl);
                        await WaitForElementByClassAsync(AIConfiguration.ClaudePromptClass);
                        break;
                    case AIModelType.DeepSeek:
                        webView.Source = new Uri(AIConfiguration.DeepSeekUrl);
                        await WaitForElementByIdAsync(AIConfiguration.DeepSeekPromptId);
                        break;
                }

                webView.WebMessageReceived += WebView_WebMessageReceived;

                await _joinableTaskFactory.SwitchToMainThreadAsync();
                await StartTimerAsync();

                _ = CheckTimerStatusAsync();

                Unloaded += OnControlUnloaded;
            }
            catch (Exception ex)
            {
                // Em caso de erro, marca como não inicializado
                lock (_initializationLock)
                {
                    _isWebViewInitialized = false;
                }

                Debug.WriteLine("Error in InitializeAsync(): " + ex.Message);
                Debug.WriteLine($"Stack Trace: {ex.StackTrace}");

                // Se falhar criar o WebView2, mostra uma mensagem mais específica
                if (ex.Message.Contains("WebView2") || ex.Message.Contains("directory"))
                {
                    MessageBox.Show(
                        $"Erro ao inicializar WebView2.\n\nDetalhes: {ex.Message}\n\nVerifique se o WebView2 Runtime está instalado e tente reiniciar o Visual Studio.",
                        "Erro de Inicialização",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        public string GetEdgeWebView2Path()
        {
            // First, try to find WebView2 Runtime
            string[] possiblePaths = {
        @"C:\Program Files (x86)\Microsoft\EdgeWebView\Application",
        @"C:\Program Files\Microsoft\EdgeWebView\Application",
        Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Microsoft\EdgeWebView\Application"),
        Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Microsoft\EdgeWebView\Application")
    };

            foreach (var basePath in possiblePaths)
            {
                if (Directory.Exists(basePath))
                {
                    var versionDirs = Directory.GetDirectories(basePath);
                    if (versionDirs.Length > 0)
                    {
                        // Return the first version directory found
                        Debug.WriteLine($"WebView2 found at: {versionDirs[0]}");
                        return versionDirs[0];
                    }
                }
            }

            // If WebView2 Runtime not found, try Edge browser paths
            string[] edgePaths = {
        @"C:\Program Files (x86)\Microsoft\Edge\Application",
        @"C:\Program Files\Microsoft\Edge\Application"
    };

            foreach (var path in edgePaths)
            {
                if (Directory.Exists(path))
                {
                    Debug.WriteLine($"Edge browser found at: {path}");
                    return path;
                }
            }

            Debug.WriteLine("WebView2/Edge path not found, returning null");
            return null;
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

            string selectedCode = GetSelectedCodeFromVS();
            bool attachedFile = await IsFileAttachedAsync();

            if (string.IsNullOrEmpty(selectedCode) && attachedFile)
            {
                selectedCode = "(attached)";
            }

            if (string.IsNullOrEmpty(selectedCode))
            {
                return;
            }

            string activeLanguage = GetActiveFileLanguage();
            string fullPrompt;

            if (!string.IsNullOrEmpty(extraCommand))
            {
                // Ensure we're using a case-insensitive replacement
                string processedExtraCommand = extraCommand.Replace("{languageCode}", activeLanguage);
                fullPrompt = processedExtraCommand + "\r\n\r\n" + selectedCode;
            }
            else
            {
                fullPrompt = selectedCode;
            }

            string script = string.Empty;

            switch (_aiModelType)
            {
                case AIModelType.GPT:
                    script = GPTConfiguration.Instance.GetSetPromptScript(fullPrompt);
                    break;
                case AIModelType.Gemini:
                    script = GeminiConfiguration.Instance.GetSetPromptScript(fullPrompt);
                    break;
                case AIModelType.Claude:
                    script = ClaudeConfiguration.Instance.GetSetPromptScript(fullPrompt);
                    break;
                case AIModelType.DeepSeek:
                    script = DeepSeekConfiguration.Instance.GetSetPromptScript(fullPrompt);
                    break;
            }

            await webView.CoreWebView2.ExecuteScriptAsync(script);

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
            if (e.Key == Key.Home || e.Key == Key.End)
            {
                e.Handled = true;

                bool shiftPressed = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
                string key = e.Key.ToString();

                string script = string.Empty;

                switch (_aiModelType)
                {
                    case AIModelType.GPT:
                        script = e.Key == Key.Home
                            ? GPTConfiguration.Instance.GetHomeKeyScript(shiftPressed, 0)
                            : GPTConfiguration.Instance.GetEndKeyScript(shiftPressed, int.MaxValue);
                        break;
                    case AIModelType.Gemini:
                        script = GeminiConfiguration.Instance.GetHomeEndKeyScript(key, shiftPressed);
                        break;
                    case AIModelType.Claude:
                        script = ClaudeConfiguration.Instance.GetHomeEndKeyScript(key, shiftPressed);
                        break;
                    case AIModelType.DeepSeek:
                        script = DeepSeekConfiguration.Instance.GetHomeEndKeyScript(key, shiftPressed);
                        break;
                }

                if (!string.IsNullOrEmpty(script))
                {
                    await webView.ExecuteScriptAsync(script);
                }
            }
        }

        private async Task AddHandlerCopyCodeAsync()
        {
            try
            {
                string script = @"if (!document.body.hasAttribute('data-keydown-listener-added')) {
                            document.addEventListener('keydown', function(event) {
                                if (event.keyCode == 13 && !event.shiftKey) {
                                    window.chrome.webview.postMessage('EnterPressed');
                                }
                            });
                            document.body.setAttribute('data-keydown-listener-added', 'true');
                        }";

                await webView.ExecuteScriptAsync(script);

                string addEventListenersScript = string.Empty;

                switch (_aiModelType)
                {
                    case AIModelType.GPT:
                        addEventListenersScript = GPTConfiguration.Instance.GetAddEventListenersScript();
                        break;
                    case AIModelType.Gemini:
                        addEventListenersScript = GeminiConfiguration.Instance.GetAddEventListenersScript();
                        break;
                    case AIModelType.Claude:
                        addEventListenersScript = ClaudeConfiguration.Instance.GetAddEventListenersScript();
                        break;
                    case AIModelType.DeepSeek:
                        addEventListenersScript = DeepSeekConfiguration.Instance.GetAddEventListenersScript();
                        break;
                }

                await webView.ExecuteScriptAsync(addEventListenersScript);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error in AddHandlerCopyCode(): " + ex.Message);
            }
        }

        private async Task SubmitPromptAIAsync()
        {
            string script = string.Empty;

            switch (_aiModelType)
            {
                case AIModelType.GPT:
                    script = GPTConfiguration.Instance.GetSubmitPromptScript();
                    await webView.ExecuteScriptAsync(script);
                    await Task.Delay(3000);
                    script = GPTConfiguration.Instance.GetScrollToBottomScript();
                    await webView.ExecuteScriptAsync(script);
                    await Task.Delay(2000);
                    await webView.ExecuteScriptAsync(script);
                    break;
                case AIModelType.Gemini:
                    script = GeminiConfiguration.Instance.GetSubmitPromptScript();
                    await webView.ExecuteScriptAsync(script);
                    break;
                case AIModelType.Claude:
                    script = ClaudeConfiguration.Instance.GetSubmitPromptScript();
                    await webView.ExecuteScriptAsync(script);
                    break;
                case AIModelType.DeepSeek:
                    script = DeepSeekConfiguration.Instance.GetSubmitPromptScript();
                    await webView.ExecuteScriptAsync(script);
                    break;
            }
        }

        private async Task<bool> IsFileAttachedAsync()
        {
            string script = string.Empty;

            switch (_aiModelType)
            {
                case AIModelType.GPT:
                    script = GPTConfiguration.Instance.GetIsFileAttachedScript();
                    break;
                case AIModelType.Claude:
                    script = ClaudeConfiguration.Instance.GetIsFileAttachedScript();
                    break;
                case AIModelType.DeepSeek:
                    script = DeepSeekConfiguration.Instance.GetIsFileAttachedScript();
                    break;
                case AIModelType.Gemini:
                    return false; // Gemini doesn't support file attachments
            }

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
                    // Handle enter key to send the prompt to AI except for Gemini
                    case "EnterPressed":
                        if (_aiModelType != AIModelType.Gemini)
                        {
                            await SubmitPromptAIAsync();
                        }
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

            //Write text file in c:\temp with project kind guid for debugging
            //File.WriteAllText("c:\\temp\\projectkind.txt", projectKind);

            switch (projectKind)
            {
                case "{F184B08F-C81C-45F6-A57F-5ABD9991F28F}": // Visual Basic Project
                    return ".vb";
                case "{E6FDF86B-F3D1-11D4-8576-0002A516ECE8}": // JavaScript Project
                    return ".js";
                case "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}": // C++ Project
                    return ".cpp";
                case "{A1591282-1198-4647-A2B1-27E5FF5F6F3B}": // TypeScript Project
                    return ".ts";
                case "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}": // C# Project
                    return ".cs";
                case "{888888a0-9f3d-457c-b088-3a5042f75d52}": // Python Tools for Visual Studio
                    return ".py";
                default:
                    return ""; // Default to empty if project type is unknown
            }
        }

        private string PromptForFileName(string defaultExtension)
        {
            var inputDialog = new InputDialog("New File", "Enter new file name", $"{defaultExtension}");
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
                // string promptCompleteCode = "Please show new full complete code without explanations with complete methods implementation for the provided code without any placeholders like ... or assuming code segments. Do not create methods you dont know. Keep all original comments.\r\n\r\n";
                string promptCompleteCode = string.Format("{0}\r\n\r\n", _buttonLabels.CompleteCodePrompt);

                string script = string.Empty;

                switch (_aiModelType)
                {
                    case AIModelType.Claude:
                        script = ClaudeConfiguration.Instance.GetSetPromptScript(promptCompleteCode);
                        break;
                    case AIModelType.Gemini:
                        script = GeminiConfiguration.Instance.GetSetPromptScript(promptCompleteCode);
                        break;
                    case AIModelType.GPT:
                        script = GPTConfiguration.Instance.GetSetPromptScript(promptCompleteCode);
                        break;
                    case AIModelType.DeepSeek:
                        script = DeepSeekConfiguration.Instance.GetSetPromptScript(promptCompleteCode);
                        break;
                }

                await webView.CoreWebView2.ExecuteScriptAsync(script);
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
                    webView.Source = new Uri(AIConfiguration.GPTUrl);
                    await WaitForElementByIdAsync(AIConfiguration.GPTPromptTextAreaId);
                }
                if (_aiModelType == AIModelType.Gemini)
                {
                    webView.Source = new Uri(AIConfiguration.GeminiUrl);
                    await WaitForElementByClassAsync(AIConfiguration.GeminiPromptClass);
                }
                if (_aiModelType == AIModelType.Claude)
                {
                    webView.Source = new Uri(AIConfiguration.ClaudeUrl);
                    await WaitForElementByClassAsync(AIConfiguration.ClaudePromptClass);
                }
                if (_aiModelType == AIModelType.DeepSeek)
                {
                    webView.Source = new Uri(AIConfiguration.DeepSeekUrl);
                    await WaitForElementByClassAsync(AIConfiguration.DeepSeekPromptClass);
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

        private void ConfigureLabelsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var window = new ButtonsConfigWindow(_buttonLabels);
            if (window.ShowDialog() == true)
            {
                UpdateButtonContentAndTooltip();
            }
        }

        private void LoadComboBoxItemsKi()
        {
            pulldownKi.Items.Clear();

            int index = 0;
            foreach (AIModelType option in Enum.GetValues(typeof(AIModelType)))
            {
                string displayString = option.ToString();
                pulldownKi.Items.Add(displayString);

                if (_aiModelType == option)
                {
                    pulldownKi.SelectedIndex = index;
                }
                ++index;
            }

            try
            {
                switch (_aiModelType)
                {
                    case AIModelType.Claude:

                        break;
                    case AIModelType.Gemini:

                        break;
                    case AIModelType.GPT:

                        break;
                    case AIModelType.DeepSeek:

                        break;
                    default:
                        throw new InvalidOperationException(string.Format("The aiModelType {0} was not implemented", _aiModelType));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in LoadComboBoxItemsKi(): {ex.Message}");
            }
        }

        private void pulldownKI_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox comboBox = sender as ComboBox;

            if (null == comboBox)
            {
                return;
            }
            if (null == comboBox.SelectedItem)
            {
                return;
            }

            string selectedItemString = comboBox.SelectedItem.ToString();

            if (!Enum.TryParse(selectedItemString, out AIModelType selectedAIModelType))
            {
                System.Diagnostics.Debug.WriteLine($"Error: Could not convert '{selectedItemString}' to AIModelType.");
            }

            bool isSwitch;

            switch (selectedAIModelType)
            {
                case AIModelType.Claude:
                    btnAttachFile.Visibility = Visibility.Visible;
                    isSwitch = true;
                    break;
                case AIModelType.Gemini:
                    btnAttachFile.Visibility = Visibility.Hidden;
                    isSwitch = true;
                    break;
                case AIModelType.GPT:
                    btnAttachFile.Visibility = Visibility.Visible;
                    isSwitch = true;
                    break;
                case AIModelType.DeepSeek:
                    btnAttachFile.Visibility = Visibility.Visible;
                    isSwitch = true;
                    break;
                default:
                    isSwitch = false;
                    break;
            }

            if (isSwitch)
            {
                _aiModelType = selectedAIModelType;
                _parentToolWindow.Caption = string.Format("{0} Extension", selectedAIModelType);
                UpdateButtonContentAndTooltip();
                OnReloadAIItemClick(null, null);
                SaveConfiguration();

                if (null != CodeActionsContextMenu && 0 < CodeActionsContextMenu.Items.Count)
                {

                    foreach (var item in CodeActionsContextMenu.Items)
                    {
                        if (typeof(MenuItem) != item.GetType())
                        {
                            continue;
                        }

                        MenuItem _item = (MenuItem)item;

                        if (!_item.Header.ToString().StartsWith("Reload"))
                        {
                            continue;
                        }

                        _item.Header = string.Format("Reload {0}", selectedAIModelType);
                    }
                }
            }
            StopTimer();
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
                    var menuItem = new MenuItem
                    {
                        Header = name,
                        Tag = prompt
                    };

                    menuItem.Click += OnSendCodeMenuItemClick;
                    CodeActionsContextMenu.Items.Add(menuItem);
                }
            }

            // Add a separator line
            CodeActionsContextMenu.Items.Add(new Separator());

            // Add Reload Chat GPT menu item
            var reloadMenuItem = new MenuItem { Header = "Reload GPT" };
            reloadMenuItem.Click += OnReloadAIItemClick;
            CodeActionsContextMenu.Items.Add(reloadMenuItem);

            // Add Configure extension... menu item
            var configureMenuItem = new MenuItem { Header = "Configure extension..." };
            configureMenuItem.Click += ConfigureExtensionMenuItem_Click;
            CodeActionsContextMenu.Items.Add(configureMenuItem);

            // Add Configure button labels... menu item
            var configureLabelsMenuItem = new MenuItem { Header = "Configure buttons" };
            configureLabelsMenuItem.Click += ConfigureLabelsMenuItem_Click;
            CodeActionsContextMenu.Items.Add(configureLabelsMenuItem);

            // Add another separator before the new options
            CodeActionsContextMenu.Items.Add(new Separator());

            // Add menu About...  showing message box with app version with major and minor version
            var aboutMenuItem = new MenuItem { Header = "About..." };
            aboutMenuItem.Click += (s, e) =>
            {
                string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(2);
                MessageBox.Show($"ChatGPT Extension Version: {version}", "About ChatGPT Extension", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            CodeActionsContextMenu.Items.Add(aboutMenuItem);

            StopTimer();
        }

        private void UpdateButtonContentAndTooltip()
        {
            // Determine the AI technology based on the AI model selected
            string aiTechnology = _aiModelType.ToString();

            // Update the content and tooltip for the buttons
            btnVSNETToAI.Content = _buttonLabels.VSNETToAI.Replace("{AI}", aiTechnology);
            btnVSNETToAI.ToolTip = $"Transfer selected code from Editor to {aiTechnology}";

            btnFixCodeInAI.Content = _buttonLabels.FixCode.Replace("{AI}", aiTechnology);
            btnFixCodeInAI.Tag = _buttonLabels.FixCodePrompt;
            btnFixCodeInAI.ToolTip = $"Fix bugs in Editor selected code using {aiTechnology}";

            btnImproveCodeInAI.Content = _buttonLabels.ImproveCode.Replace("{AI}", aiTechnology);
            btnImproveCodeInAI.Tag = _buttonLabels.ImproveCodePrompt;
            btnImproveCodeInAI.ToolTip = $"Refactor selected code from Editor in {aiTechnology}";

            btnAIToVSNET.Content = _buttonLabels.AIToVSNET.Replace("{AI}", aiTechnology);
            btnAIToVSNET.ToolTip = $"Transfer selected code from {aiTechnology} to Editor";

            btnAttachFile.Content = _buttonLabels.AttachFile.Replace("{AI}", aiTechnology);
            btnAttachFile.ToolTip = $"Attach Editor file open to {aiTechnology}";

            btnCompleteCodeInAI.Content = _buttonLabels.CompleteCode.Replace("{ai}", aiTechnology);
            btnCompleteCodeInAI.ToolTip = $"Ask {aiTechnology} to generate complete code";


            btnNewFile.Content = _buttonLabels.NewFile.Replace("{ai}", aiTechnology);
            btnNewFile.ToolTip = $"Select code in {aiTechnology} to create a new file in VS.NET";

            btnContinueCode.Content = _buttonLabels.ContinueCode.Replace("{ai}", aiTechnology);
            btnContinueCode.ToolTip = $"Ask {aiTechnology} to continue the code generation";

            EnableCopyCodeCheckBox.Content = _buttonLabels.EnableCopyCode.Replace("{ai}", aiTechnology);

            EnableCopyCodeCheckBox.ToolTip = $"Enable sending code from {aiTechnology} to Editor when Copy code button is clicked in {aiTechnology}";
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

                if (_aiModelType == AIModelType.Gemini)
                {
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
                                    string clickFileInputScript = ClaudeConfiguration.Instance.GetAttachFileScript();
                                    await webView.CoreWebView2.ExecuteScriptAsync(clickFileInputScript);
                                }
                                else if (_aiModelType == AIModelType.GPT)
                                {
                                    string clickButtonScript = GPTConfiguration.Instance.GetFileInputClickScript();
                                    await webView.CoreWebView2.ExecuteScriptAsync(clickButtonScript);
                                }
                                else if (_aiModelType == AIModelType.DeepSeek)
                                {
                                    string clickAttachScript = DeepSeekConfiguration.Instance.GetAttachFileScript();
                                    await webView.CoreWebView2.ExecuteScriptAsync(clickAttachScript);
                                }

                                // Wait for the file dialog to open
                                await Task.Delay(2500); // Adjust the delay as necessary

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
                // string promptContinueCode = "Continue code generation\r\n\r\n";
                string promptContinueCode = string.Format("{0}\r\n\r\n", _buttonLabels.ContinueCodePrompt);
                string script = string.Empty;

                switch (_aiModelType)
                {
                    case AIModelType.Claude:
                        script = ClaudeConfiguration.Instance.GetSetPromptScript(promptContinueCode);
                        break;
                    case AIModelType.Gemini:
                        script = GeminiConfiguration.Instance.GetSetPromptScript(promptContinueCode);
                        break;
                    case AIModelType.GPT:
                        script = GPTConfiguration.Instance.GetSetPromptScript(promptContinueCode);
                        break;
                    case AIModelType.DeepSeek:
                        script = DeepSeekConfiguration.Instance.GetSetPromptScript(promptContinueCode);
                        break;
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
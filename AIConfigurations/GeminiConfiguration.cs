/* *******************************************************************************************************************
 * Application: Chat GPT Extension
 * 
 * Autor:  Daniel Liedke
 * 
 * Copyright � Daniel Liedke 2025
 * Usage and reproduction in any manner whatsoever without the written permission of Daniel Liedke is strictly forbidden.
 *  
 * Purpose: All integration code for Google Gemini AI
 *           
 * *******************************************************************************************************************/

using System;
using System.Linq;

using Newtonsoft.Json;

namespace ChatGPTExtension
{

    public class GeminiConfiguration
    {
        #region Singleton

        private static GeminiConfiguration _instance;
        private static readonly object _lock = new object();

        public static GeminiConfiguration Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new GeminiConfiguration();
                        }
                    }
                }
                return _instance;
            }
        }

        private GeminiConfiguration() { }

        #endregion

        // Constants
        public const string GEMINI_URL = "https://gemini.google.com";
        public const string GEMINI_PROMPT_CLASS = "ql-editor";
        public const string GEMINI_COPY_CODE_BUTTON_CLASS = "copy-button";

        public string GetSetPromptScript(string promptText)
        {
            // First encode the HTML characters
            var htmlEncoded = System.Net.WebUtility.HtmlEncode(promptText);

            // Then do the other processing and JSON serialization
            htmlEncoded = htmlEncoded
                   .Trim('"')
                   .Replace("'", "\\'")
                   .Replace("<", "&lt;")
                   .Replace(">", "&gt;");

            var lines = htmlEncoded.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var codeHtml = string.Join("", lines.Select(line => $"<p>{line}</p>"));
            var codeHtmlJson = JsonConvert.SerializeObject(codeHtml);

            return $@"
                (function() {{
                    if (!window.myTrustedTypesPolicy) {{
                        window.myTrustedTypesPolicy = trustedTypes.createPolicy('default', {{
                            createHTML: (string) => string
                        }});
                    }}
                    const trustedHTML = window.myTrustedTypesPolicy.createHTML({codeHtmlJson});
                    var element = document.querySelector('.{AIConfiguration.GeminiPromptClass}');
                    var existingHtml = element.innerHTML;
                    var newContent = existingHtml + (existingHtml && existingHtml.trim() ? '<p><br></p>' : '') + trustedHTML;
                    element.innerHTML = newContent;
                    var inputEvent = new Event('input', {{
                        'bubbles': true,
                        'cancelable': true
                    }});
                    element.dispatchEvent(inputEvent);
                }})();";
        }

        public string GetSubmitPromptScript()
        {
            return @"
        var sendButton = document.querySelector('button.send-button[mat-icon-button]');
        if (sendButton) {
            sendButton.click();
        } else {
            console.error('Send button not found');
        }";
        }

        public string GetAddEventListenersScript()
        {
            return $@"
        var allButtons = Array.from(document.getElementsByClassName('{AIConfiguration.GeminiCopyCodeButtonClass}'));
        var targetButtons = allButtons.filter(function(button) {{
            return !button.hasAttribute('data-listener-added');
        }});

        targetButtons.forEach(function(button) {{
            button.addEventListener('click', function() {{
                window.chrome.webview.postMessage('CopyCodeButtonClicked');
            }});
            button.setAttribute('data-listener-added', 'true');
        }});";
        }

        public string GetReceiveCodeScript(string selectedCode)
        {
            var lines = selectedCode.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var codeHtml = string.Join("", lines.Select(line => $"<p>{line}</p>"));
            var codeHtmlJson = JsonConvert.SerializeObject(codeHtml);

            return $@"
        (function() {{
            if (!window.myTrustedTypesPolicy) {{
                window.myTrustedTypesPolicy = trustedTypes.createPolicy('default', {{
                    createHTML: (string) => string
                }});
            }}
            const trustedHTML = window.myTrustedTypesPolicy.createHTML({codeHtmlJson});
            var element = document.querySelector('.{AIConfiguration.GeminiPromptClass}');
            element.innerHTML = element.innerHTML + trustedHTML;
            var inputEvent = new Event('input', {{
                'bubbles': true,
                'cancelable': true
            }});
            element.dispatchEvent(inputEvent);
        }})();";
        }

        public string GetHomeEndKeyScript(string key, bool shiftPressed)
        {
            return $@"
        (function() {{
            var editor = document.querySelector('.{GEMINI_PROMPT_CLASS}');
            var selection = window.getSelection();
            if (selection.rangeCount > 0) {{
                var range = selection.getRangeAt(0);
                var node = selection.focusNode;
                var offset = selection.focusOffset;
                while (node && node.nodeType !== 3 && !['DIV', 'P', 'BR'].includes(node.nodeName)) {{
                    node = node.parentNode;
                }}
                var textContent = node.nodeType === 3 ? node.data : node.textContent;
                var position = offset;
                if ('{key}' === 'Home') {{
                    while (position > 0 && textContent[position - 1] != '\n') {{
                        position--;
                    }}
                }} else if ('{key}' === 'End') {{
                    while (position < textContent.length && textContent[position] != '\n') {{
                        position++;
                    }}
                }}
                if (node.nodeType === 3) {{
                    if (!{shiftPressed.ToString().ToLower()}) {{
                        range.setStart(node, position);
                        range.setEnd(node, position);
                    }} else {{
                        if ('{key}' === 'Home') {{
                            range.setStart(node, position);
                        }} else if ('{key}' === 'End') {{
                            range.setEnd(node, position);
                        }}
                    }}
                }} else {{
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
                                    if ('{key}' === 'Home') {{
                                        range.setStart(child, position - i);
                                    }} else if ('{key}' === 'End') {{
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
        }

        public string GetAttachFileScript()
        {
            return @"
                (function() {
                    console.log('=== GEMINI SIMPLE FILE ATTACH ===');

                    // Create a visual message for the user
                    var currentMessage = null;
                    function showUserMessage(message, duration = 5000) {
                        // Remove any existing message
                        if (currentMessage && currentMessage.parentNode) {
                            currentMessage.parentNode.removeChild(currentMessage);
                        }

                        var messageDiv = document.createElement('div');
                        messageDiv.style.cssText = `
                            position: fixed;
                            top: 20px;
                            right: 20px;
                            background: #4285f4;
                            color: white;
                            padding: 12px 20px;
                            border-radius: 8px;
                            font-family: 'Google Sans', sans-serif;
                            font-size: 14px;
                            z-index: 10000;
                            box-shadow: 0 4px 12px rgba(0,0,0,0.3);
                        `;
                        messageDiv.textContent = message;
                        document.body.appendChild(messageDiv);
                        currentMessage = messageDiv;

                        setTimeout(function() {
                            if (messageDiv.parentNode) {
                                messageDiv.parentNode.removeChild(messageDiv);
                                if (currentMessage === messageDiv) {
                                    currentMessage = null;
                                }
                            }
                        }, duration);
                    }


                    // Monitor for upload menu clicks
                    function setupMenuClickDetection() {
                        // Look for the specific upload menu button
                        var fileUploadButton = document.querySelector('[data-test-id=""local-image-file-uploader-button""]');

                        if (fileUploadButton) {
                            console.log('✅ Found upload button, setting up click detection');

                            // Override the click function
                            var originalClick = fileUploadButton.click;
                            fileUploadButton.click = function() {
                                console.log('✅ Upload menu clicked! Showing Open button message');
                                showUserMessage('Now click the ""Open"" button in the file dialog', 8000);
                                return originalClick.apply(this, arguments);
                            };

                            // Also listen for actual click events
                            fileUploadButton.addEventListener('click', function() {
                                console.log('✅ Upload menu clicked via event! Showing Open button message');
                                setTimeout(function() {
                                    showUserMessage('Now click the ""Open"" button in the file dialog', 8000);
                                }, 1000);
                            });

                            showUserMessage('Ready! Click ""Upload files"" to start.', 5000);
                            return true;
                        }
                        return false;
                    }

                    // Try to find the upload button
                    function findUploadButton() {
                        var attempts = 0;
                        var maxAttempts = 20;

                        function checkForButton() {
                            attempts++;

                            if (setupMenuClickDetection()) {
                                return; // Found and set up
                            }

                            if (attempts < maxAttempts) {
                                setTimeout(checkForButton, 500);
                            } else {
                                console.log('⏰ Could not find upload button, manual process needed');
                                showUserMessage('Please click + button, then ""Upload files"", then ""Open""', 8000);
                            }
                        }

                        checkForButton();
                    }

                    // Start the process
                    function startProcess() {
                        console.log('Starting simple file attach process...');

                        // Try to click the + button first
                        var addButton = document.querySelector('button[aria-label*=""upload""], button.upload-card-button, button[data-test-id*=""uploader""]');

                        if (!addButton) {
                            var addIcon = document.querySelector('mat-icon[data-mat-icon-name=""add_2""], mat-icon[fonticon=""add_2""]');
                            if (addIcon) {
                                addButton = addIcon.closest('button');
                            }
                        }

                        if (addButton) {
                            console.log('✅ Found + button, clicking it');
                            addButton.click();
                            showUserMessage('Upload menu opening...', 2000);
                            setTimeout(findUploadButton, 500);
                        } else {
                            console.log('❌ + button not found, waiting for manual action');
                            showUserMessage('Please click the + button manually', 5000);
                            findUploadButton();
                        }
                    }

                    // Start immediately
                    startProcess();

                })();";
        }
    }
}
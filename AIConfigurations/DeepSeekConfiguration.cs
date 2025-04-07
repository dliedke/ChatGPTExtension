/* *******************************************************************************************************************
 * Application: ChatGPTExtension
 * 
 * Autor:  Daniel Liedke
 * 
 * Copyright © Daniel Liedke 2025
 * Usage and reproduction in any manner whatsoever without the written permission of Daniel Liedke is strictly forbidden.
 *  
 * Purpose: All integration code for DeepSeek AI
 *           
 * *******************************************************************************************************************/

using System;
using System.Linq;
using Newtonsoft.Json;

namespace ChatGPTExtension
{
    public class DeepSeekConfiguration
    {
        #region Singleton

        private static DeepSeekConfiguration _instance;
        private static readonly object _lock = new object();

        public static DeepSeekConfiguration Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new DeepSeekConfiguration();
                        }
                    }
                }
                return _instance;
            }
        }

        private DeepSeekConfiguration() { }

        #endregion

        // Constants
        public const string DEEPSEEK_URL = "https://chat.deepseek.com/";
        public const string DEEPSEEK_PROMPT_ID = "chat-input";
        public const string DEEPSEEK_PROMPT_CLASS = "_27c9245";
        public const string DEEPSEEK_COPY_CODE_BUTTON_CLASS = "ds-markdown-code-copy-button";
        public const string DEEPSEEK_SEND_BUTTON_SELECTOR = "div[role=\"button\"]:not([aria-disabled=\"true\"]) .ds-icon svg[viewBox=\"0 0 14 16\"]";
        public const string DEEPSEEK_ATTACH_FILE_SELECTOR = "div.ds-icon svg[viewBox=\"0 0 14 20\"]";

        public string GetSetPromptScript(string promptText)
        {
            // Standard JSON serialization to escape properly for JS
            var escapedPrompt = JsonConvert.SerializeObject(promptText)
                .Trim('"')
                .Replace("'", "\\'");

            return $@"
        (function() {{
            var textarea = document.getElementById('{AIConfiguration.DeepSeekPromptId}');
            if (textarea) {{
                // Set the value
                var existingText = textarea.value;
                textarea.value = existingText + (existingText && existingText.trim() ? '\n\n' : '') + '{escapedPrompt}';
                
                // Focus the textarea
                textarea.focus();
                
                // Create and dispatch comprehensive events
                
                // 1. Input event - most important for content changes
                var inputEvent = new InputEvent('input', {{
                    bubbles: true,
                    cancelable: true,
                    data: '{escapedPrompt}'
                }});
                textarea.dispatchEvent(inputEvent);
                
                // 2. KeyDown and KeyUp events with realistic values
                var lastChar = '{escapedPrompt}'.slice(-1);
                var keyEvents = ['keydown', 'keyup'];
                keyEvents.forEach(function(eventType) {{
                    var keyEvent = new KeyboardEvent(eventType, {{
                        bubbles: true,
                        cancelable: true,
                        key: lastChar,
                        code: 'Key' + lastChar.toUpperCase(),
                        keyCode: lastChar.charCodeAt(0),
                        which: lastChar.charCodeAt(0),
                        composed: true
                    }});
                    textarea.dispatchEvent(keyEvent);
                }});
                
                // 3. Change event
                var changeEvent = new Event('change', {{
                    bubbles: true,
                    cancelable: true
                }});
                textarea.dispatchEvent(changeEvent);
                
                // 4. Force DeepSeek to recognize the content by selecting in the field
                textarea.selectionStart = textarea.value.length;
                textarea.selectionEnd = textarea.value.length;
                
                // 5. Directly check and modify the button state
                setTimeout(function() {{
                    var sendButton = document.querySelector('{AIConfiguration.DeepSeekSendButtonSelector}');
                    if (sendButton) {{
                        var buttonContainer = sendButton.closest('div[role=""button""]');
                        if (buttonContainer) {{
                            // Remove disabled attributes if present
                            if (buttonContainer.getAttribute('aria-disabled') === 'true') {{
                                buttonContainer.setAttribute('aria-disabled', 'false');
                                buttonContainer.classList.remove('_disabled');
                                buttonContainer.style.opacity = '1';
                                buttonContainer.style.cursor = 'pointer';
                            }}
                        }}
                    }}
                }}, 100);
            }} else {{
                console.error('DeepSeek prompt area not found');
            }}
        }})();";
        }

        public string GetSubmitPromptScript()
        {
            return @"
        // This tries to find a button that is NOT disabled, which won't work for short prompts
        var sendButton = document.querySelector('div[role=""button""] .ds-icon svg[viewBox=""0 0 14 16""]');
        if (sendButton) {
            var buttonContainer = sendButton.closest('div[role=""button""]');
            if (buttonContainer) {
                // Force enable the button regardless of its current state
                buttonContainer.setAttribute('aria-disabled', 'false');
                
                // Remove any disabled classes that may be present
                buttonContainer.classList.remove('_disabled');
                buttonContainer.style.opacity = '1';
                buttonContainer.style.cursor = 'pointer';
                
                // Now click the button
                buttonContainer.click();
                
                // As a fallback, try to trigger an Enter key press on the textarea
                var textarea = document.getElementById('chat-input');
                if (textarea) {
                    var enterEvent = new KeyboardEvent('keydown', {
                        bubbles: true,
                        cancelable: true,
                        key: 'Enter',
                        code: 'Enter',
                        keyCode: 13,
                        which: 13
                    });
                    textarea.dispatchEvent(enterEvent);
                }
            }
        } else {
            console.error('Send button not found');
        }";
        }

        public string GetAttachFileScript()
        {
            return $@"
                var attachButton = document.querySelector('{AIConfiguration.DeepSeekAttachFileSelector}');
                if (attachButton) {{
                    attachButton.closest('div.ds-icon').click();
                }} else {{
                    console.error('Attach file button not found');
                }}";
        }

        public string GetIsFileAttachedScript()
        {
            return @"
                var result = 'notfound';
                var fileIcons = document.querySelectorAll('div.ds-icon svg[viewBox=""0 0 32 32""]');
                if (fileIcons.length > 0) {
                    result = 'found';
                }
                result;";
        }

        public string GetAddEventListenersScript()
        {
            return $@"
                function addClickListener(button, message) {{
                    if (!button.hasAttribute('data-listener-added')) {{
                        button.addEventListener('click', function() {{
                            window.chrome.webview.postMessage(message);
                        }});
                        button.setAttribute('data-listener-added', 'true');
                    }}
                }}

                // Find all copy buttons
                var copyButtons = document.querySelectorAll('.{AIConfiguration.DeepSeekCopyCodeButtonClass}');
                copyButtons.forEach(function(button) {{
                    addClickListener(button, 'CopyCodeButtonClicked');
                }});

                // Set up an observer to handle dynamically created buttons
                var observer = new MutationObserver(function(mutations) {{
                    mutations.forEach(function(mutation) {{
                        if (mutation.type === 'childList') {{
                            mutation.addedNodes.forEach(function(node) {{
                                if (node.nodeType === Node.ELEMENT_NODE) {{
                                    var newButtons = node.querySelectorAll('.{AIConfiguration.DeepSeekCopyCodeButtonClass}');
                                    newButtons.forEach(function(button) {{
                                        addClickListener(button, 'CopyCodeButtonClicked');
                                    }});
                                }}
                            }});
                        }}
                    }});
                }});

                observer.observe(document.body, {{ childList: true, subtree: true }});";
        }

        public string GetHomeEndKeyScript(string key, bool shiftPressed)
        {
            return $@"
                (function() {{
                    var textarea = document.getElementById('{AIConfiguration.DeepSeekPromptId}');
                    if (textarea) {{
                        var textBeforeCursor = textarea.value.substring(0, textarea.selectionStart);
                        var textAfterCursor = textarea.value.substring(textarea.selectionEnd);
                        
                        // Find the position to move to
                        var position;
                        if ('{key}' === 'Home') {{
                            var lastNewline = textBeforeCursor.lastIndexOf('\n');
                            position = lastNewline !== -1 ? lastNewline + 1 : 0;
                        }} else if ('{key}' === 'End') {{
                            var nextNewline = textAfterCursor.indexOf('\n');
                            position = nextNewline !== -1 ? 
                                      textarea.selectionStart + nextNewline : 
                                      textarea.value.length;
                        }}

                        // Set the cursor position
                        if (!{shiftPressed.ToString().ToLower()}) {{
                            textarea.setSelectionRange(position, position);
                        }} else {{
                            if ('{key}' === 'Home') {{
                                textarea.setSelectionRange(position, textarea.selectionEnd);
                            }} else if ('{key}' === 'End') {{
                                textarea.setSelectionRange(textarea.selectionStart, position);
                            }}
                        }}
                    }}
                }})();";
        }
    }
}
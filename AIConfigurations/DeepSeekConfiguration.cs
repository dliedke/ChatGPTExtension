/* *******************************************************************************************************************
 * Application: Chat GPT Extension
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
        public const string DEEPSEEK_PROMPT_CLASS = "_27c9245 ds-scroll-area";
        public const string DEEPSEEK_COPY_CODE_BUTTON_SELECTOR = "span.code-info-button-text";
        public const string DEEPSEEK_SEND_BUTTON_SELECTOR = "div[role=\"button\"] svg[viewBox=\"0 0 16 16\"] path[d^=\"M8.3125 0.981648C8.66767\"]";
        public const string DEEPSEEK_ATTACH_FILE_SELECTOR = "div._17e543b.f02f0e25";

        public string GetSetPromptScript(string promptText)
        {
            // Standard JSON serialization to escape properly for JS
            var escapedPrompt = JsonConvert.SerializeObject(promptText)
                .Trim('"')
                .Replace("'", "\\'");

            return $@"
        (function() {{
            var textarea = document.getElementById('{AIConfiguration.DeepSeekPromptId}') || document.querySelector('textarea._27c9245.ds-scroll-area');
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
                
                // 5. No need to modify button state since we use Enter key to send
            }} else {{
                console.error('DeepSeek prompt area not found');
            }}
        }})();";
        }

        public string GetSubmitPromptScript()
        {
            return @"
        // Simple approach: use Enter key on textarea to send prompt
        var textarea = document.getElementById('chat-input') || document.querySelector('textarea._27c9245.ds-scroll-area');
        if (textarea) {
            // Focus the textarea first
            textarea.focus();
            
            // Send Enter key press
            var enterEvent = new KeyboardEvent('keydown', {
                bubbles: true,
                cancelable: true,
                key: 'Enter',
                code: 'Enter',
                keyCode: 13,
                which: 13
            });
            textarea.dispatchEvent(enterEvent);
            
            // Also try keyup event
            var enterUpEvent = new KeyboardEvent('keyup', {
                bubbles: true,
                cancelable: true,
                key: 'Enter',
                code: 'Enter',
                keyCode: 13,
                which: 13
            });
            textarea.dispatchEvent(enterUpEvent);
        } else {
            console.error('DeepSeek textarea not found');
        }";
        }

        public string GetAttachFileScript()
        {
            return $@"
                // Direct approach using working selector
                var attachButton = document.querySelector('{AIConfiguration.DeepSeekAttachFileSelector}');
                if (attachButton) {{
                    attachButton.click();
                }} else {{
                    // Fallback: directly click hidden file input
                    var fileInput = document.querySelector('input[type=""file""]');
                    if (fileInput) {{
                        fileInput.click();
                    }} else {{
                        console.error('Attach file functionality not found');
                    }}
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
                function addClickListener(element, message) {{
                    if (!element.hasAttribute('data-listener-added')) {{
                        element.addEventListener('click', function() {{
                            window.chrome.webview.postMessage(message);
                        }});
                        element.setAttribute('data-listener-added', 'true');
                    }}
                }}

                // Find ALL buttons with Copy text and add listeners
                var allButtons = document.querySelectorAll('button');
                allButtons.forEach(function(button) {{
                    if (button.textContent && button.textContent.trim() === 'Copy') {{
                        addClickListener(button, 'CopyCodeButtonClicked');
                    }}
                }});
                
                // Also check span-based approach for additional coverage
                var copySpans = document.querySelectorAll('{AIConfiguration.DeepSeekCopyCodeButtonSelector}');
                copySpans.forEach(function(span) {{
                    if (span.textContent.trim() === 'Copy') {{
                        var button = span.closest('button');
                        if (button) {{
                            addClickListener(button, 'CopyCodeButtonClicked');
                        }}
                    }}
                }});

                // Set up an observer to handle dynamically created copy buttons
                var observer = new MutationObserver(function(mutations) {{
                    mutations.forEach(function(mutation) {{
                        if (mutation.type === 'childList') {{
                            mutation.addedNodes.forEach(function(node) {{
                                if (node.nodeType === Node.ELEMENT_NODE) {{
                                    // Handle ALL new buttons with Copy text
                                    var newButtons = node.querySelectorAll('button');
                                    newButtons.forEach(function(button) {{
                                        if (button.textContent && button.textContent.trim() === 'Copy') {{
                                            addClickListener(button, 'CopyCodeButtonClicked');
                                        }}
                                    }});
                                    
                                    // Also handle span-based copy buttons
                                    var newSpans = node.querySelectorAll('{AIConfiguration.DeepSeekCopyCodeButtonSelector}');
                                    newSpans.forEach(function(span) {{
                                        if (span.textContent.trim() === 'Copy') {{
                                            var button = span.closest('button');
                                            if (button) {{
                                                addClickListener(button, 'CopyCodeButtonClicked');
                                            }}
                                        }}
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
                    var textarea = document.getElementById('{AIConfiguration.DeepSeekPromptId}') || document.querySelector('textarea._27c9245.ds-scroll-area');
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
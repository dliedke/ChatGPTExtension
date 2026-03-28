/* *******************************************************************************************************************
 * Application: Chat GPT Extension
 * 
 * Autor:  Daniel Liedke
 * 
 * Copyright � Daniel Liedke 2025
 * Usage and reproduction in any manner whatsoever without the written permission of Daniel Liedke is strictly forbidden.
 *  
 * Purpose: All integration code for Open AI Chat GPT
 *           
 * *******************************************************************************************************************/

using System;
using System.Linq;
using Newtonsoft.Json;

namespace ChatGPTExtension
{
    public class GPTConfiguration
    {
        #region Singleton

        private static GPTConfiguration _instance;
        private static readonly object _lock = new object();

        public static GPTConfiguration Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new GPTConfiguration();
                        }
                    }
                }
                return _instance;
            }
        }

        private GPTConfiguration() { }

        #endregion

        // Constants
        public const string CHAT_GPT_URL = "https://chatgpt.com/";
        public const string GPT_PROMPT_TEXT_AREA_ID = "prompt-textarea";
        public const string GPT_COPY_CODE_BUTTON_SELECTOR = "button.flex.gap-1.items-center.select-none[data-state=\"closed\"]";
        public const string GPT_COPY_CODE_BUTTON_ICON_SELECTOR = "button.flex.gap-1.items-center.select-none[data-state=\"closed\"] svg";
        public const string GPT_CANVAS_COPY_BUTTON_SELECTOR = "button.no-draggable.text-token-text-primary";
        public const string GPT_BLACKBOARD_COPY_BUTTON_SELECTOR = "button.no-draggable.text-token-text-primary";

        public string GetSetPromptScript(string promptText)
        {
            // First encode the HTML characters
            var htmlEncoded = System.Net.WebUtility.HtmlEncode(promptText);

            // Then do the JSON serialization and other processing
            var escapedPrompt = JsonConvert.SerializeObject(htmlEncoded)
                .Trim('"')
                .Replace("'", "\\'")
                .Split(new[] { "\\r\\n", "\\n" }, StringSplitOptions.None)
                .Select(line => $"<p>{line}</p>")
                .Aggregate((current, next) => current + next);

            return $@"
                var promptArea = document.querySelector('div[id=""{AIConfiguration.GPTPromptTextAreaId}""]');
                if (promptArea) {{
                    var existingText = promptArea.innerHTML;
                    var newContent = existingText + (existingText && existingText.trim() ? '<p><br></p>' : '') + '{escapedPrompt}';
                    promptArea.innerHTML = newContent;
                    var inputEvent = new Event('input', {{
                        'bubbles': true,
                        'cancelable': true
                    }});
                    promptArea.dispatchEvent(inputEvent);
                }} else {{
                    console.error('GPT prompt area not found');
                }}";
        }

        public string GetSubmitPromptScript()
        {
            return @"document.querySelector('button[data-testid=""send-button""]').click();";
        }

        public string GetScrollToBottomScript()
        {
            return @"
            var button = document.querySelector('button.cursor-pointer.absolute.z-10.bottom-\\[calc\\(var\\(--composer-overlap-px\\)\\+--spacing\\(6\\)\\)\\]');
            if (button) {
                button.click();
            }";
        }


        public string GetFileInputClickScript()
        {
            return @"
            var input = document.querySelector('input[type=""file""][multiple]');
            if (input) {
                input.click();
            }";
        }

        public string GetAttachFileMenuItemClickScript()
        {
            return @"
setTimeout(() => {
    const menuItems = document.querySelectorAll('div[role=""menuitem""]');
    menuItems.forEach(item => {
        const svgPath = item.querySelector('svg path[d*=""M14.3352 17.5003V15.6654H12.5002""]');
        if (svgPath) {
            item.click();
        }
    });
}, 800);";
        }


        public string GetIsFileAttachedScript()
        {
            return @"
        var result = 'notfound';
        var divs = document.querySelectorAll('div.truncate.font-semibold');
        for (var i = 0; i < divs.length; i++) {
            if (divs[i].textContent.startsWith('GPTExtension_')) {
                result = 'found';
                break;
            }
        }
        result;";
        }

        public string GetAddEventListenersScript()
        {
            return $@"
        var buttonSelector = '{AIConfiguration.GPTCopyCodeButtonSelector}';
        var iconSelector = '{AIConfiguration.GPTCopyCodeButtonIconSelector}';
        var canvasCopyButtonSelector = '{AIConfiguration.GPTCanvasCopyButtonSelector.Replace("\\", "\\\\").Replace("'", "\\'")}';
        var blackboardCopyButtonSelector = '{AIConfiguration.GPTBlackboardCopyButtonSelector.Replace("\\", "\\\\").Replace("'", "\\'")}';
        var newDesignSelector = 'button.flex.gap-1.items-center.select-none[data-state=""closed""]';
        var newCanvasSelector = 'button.no-draggable.text-token-text-primary';
        var combinedSelector = buttonSelector + ', ' + newDesignSelector + ', ' + canvasCopyButtonSelector + ', ' + blackboardCopyButtonSelector + ', ' + newCanvasSelector;

        function handleButtonClick(event) {{
            var button = event.target.closest('button');
            if (button && !button.hasAttribute('data-custom-click-handled')) {{
                window.chrome.webview.postMessage('CopyCodeButtonClicked');
                button.setAttribute('data-custom-click-handled', 'true');
            }}
        }}

        function addListenerToButton(button) {{
            if (!button.hasAttribute('data-listener-added')) {{
                button.addEventListener('click', handleButtonClick, true);

                var buttonText = button.querySelector('svg + *');
                if (buttonText) {{
                    buttonText.addEventListener('click', handleButtonClick, true);
                }}

                button.setAttribute('data-listener-added', 'true');
            }}
        }}

        var allButtons = Array.from(document.querySelectorAll(combinedSelector));
        var allIcons = Array.from(document.querySelectorAll(iconSelector));

        allButtons.forEach(addListenerToButton);
        allIcons.forEach(function(icon) {{
            icon.addEventListener('click', handleButtonClick, true);
        }});

        var observer = new MutationObserver(function(mutations) {{
            mutations.forEach(function(mutation) {{
                if (mutation.type === 'childList') {{
                    mutation.addedNodes.forEach(function(node) {{
                        if (node.nodeType === Node.ELEMENT_NODE) {{
                            if (node.matches && node.matches(combinedSelector)) {{
                                addListenerToButton(node);
                            }}
                            if (node.querySelectorAll) {{
                                var newButtons = node.querySelectorAll(combinedSelector);
                                newButtons.forEach(addListenerToButton);
                            }}
                        }}
                    }});
                }}
            }});
        }});

        observer.observe(document.body, {{ childList: true, subtree: true }});";
        }

        public string GetHomeKeyScript(bool shiftPressed, int startOfLine)
        {
            return $@"
        (function() {{
            var editor = document.querySelector('div[id=""{AIConfiguration.GPTPromptTextAreaId}""]');
            var selection = window.getSelection();
            var range = selection.getRangeAt(0);
            var node = range.startContainer;
            while (node && node.nodeType !== 3) {{
                node = node.firstChild;
            }}
            if ({(shiftPressed ? "true" : "false")}) {{
                range.setStart(node, {startOfLine});
            }} else {{
                range.setStart(node, {startOfLine});
                range.setEnd(node, {startOfLine});
            }}
            selection.removeAllRanges();
            selection.addRange(range);
        }})();";
        }

        public string GetEndKeyScript(bool shiftPressed, int endOfLine)
        {
            return $@"
    (function() {{
        var editor = document.querySelector('div[id=""{AIConfiguration.GPTPromptTextAreaId}""]');
        var selection = window.getSelection();
        var range = selection.getRangeAt(0);
        var node = range.startContainer;

        while (node && node.nodeType !== 3) {{
            node = node.firstChild;
        }}

        if (node) {{
            var textContent = node.textContent;
            var position = textContent.length;

            // Find the end of the current line
            while (position > 0 && textContent[position - 1] !== '\n') {{
                position--;
            }}
            position = textContent.length - position;

            if ({(shiftPressed ? "true" : "false")}) {{
                range.setEnd(node, position);
            }} else {{
                range.setStart(node, position);
                range.setEnd(node, position);
            }}

            selection.removeAllRanges();
            selection.addRange(range);

            // Scroll the editor to make the cursor visible
            var rect = range.getBoundingClientRect();
            editor.scrollTop = rect.top - editor.offsetTop;
        }}
    }})();";
        }
    }
}

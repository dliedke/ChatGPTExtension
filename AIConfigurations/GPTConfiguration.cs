/* *******************************************************************************************************************
 * Application: ChatGPTExtension
 * 
 * Autor:  Daniel Liedke
 * 
 * Copyright © Daniel Liedke 2025
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
        public const string GPT_COPY_CODE_BUTTON_SELECTOR = "button.flex.gap-1.items-center.select-none.py-1";
        public const string GPT_COPY_CODE_BUTTON_ICON_SELECTOR = "button.flex.gap-1.items-center svg.icon-sm";
        public const string GPT_CANVAS_COPY_BUTTON_SELECTOR = "div.flex.select-none.items-center.leading-\\[0\\].gap-2 > span[data-state=\"closed\"] > button:has(svg.icon-xl-heavy path[d=\"M7 5C7 3.34315 8.34315 2 10 2H19C20.6569 2 22 3.34315 22 5V14C22 15.6569 20.6569 17 19 17H17V19C17 20.6569 15.6569 22 14 22H5C3.34315 22 2 20.6569 2 19V10C2 8.34315 3.34315 7 5 7H7V5ZM9 7H14C15.6569 7 17 8.34315 17 10V15H19C19.5523 15 20 14.5523 20 14V5C20 4.44772 19.5523 4 19 4H10C9.44772 4 9 4.44772 9 5V7ZM5 9C4.44772 9 4 9.44772 4 10V19C4 19.5523 4.44772 20 5 20H14C14.5523 20 15 19.5523 15 19V10C15 9.44772 14.5523 9 14 9H5Z\"])";

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
        var button = document.querySelector('button.absolute[class*=""bottom-5""] svg.icon-md');
        if (button) {
            button.parentElement.click();
        }";
        }

        public string GetAttachFileButtonClickScript()
        {
            return @"document
                  .querySelector('button svg path[d=""M12 3C12.5523 3 13 3.44772 13 4L13 11H20C20.5523 11 21 11.4477 21 12C21 12.5523 20.5523 13 20 13L13 13L13 20C13 20.5523 12.5523 21 12 21C11.4477 21 11 20.5523 11 20L11 13L4 13C3.44772 13 3 12.5523 3 12C3 11.4477 3.44772 11 4 11L11 11L11 4C11 3.44772 11.4477 3 12 3Z""]')
                  ?.closest('button')
                  ?.click();";
        }

        public string GetAttachFileMenuItemClickScript()
        {
            return @"
        setTimeout(() => {
            const menuItems = document.querySelectorAll('div[role=""menuitem""]');
            if (menuItems?.length > 0) {
                menuItems[menuItems.length - 1]?.click();
            }
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

        var allButtons = Array.from(document.querySelectorAll(buttonSelector));
        var allIcons = Array.from(document.querySelectorAll(iconSelector));
        var canvasCopyButtons = Array.from(document.querySelectorAll(canvasCopyButtonSelector));

        allButtons.forEach(addListenerToButton);
        allIcons.forEach(function(icon) {{
            icon.addEventListener('click', handleButtonClick, true);
        }});
        canvasCopyButtons.forEach(addListenerToButton);

        var observer = new MutationObserver(function(mutations) {{
            mutations.forEach(function(mutation) {{
                if (mutation.type === 'childList') {{
                    mutation.addedNodes.forEach(function(node) {{
                        if (node.nodeType === Node.ELEMENT_NODE) {{
                            if (node.matches(buttonSelector) || node.matches(canvasCopyButtonSelector)) {{
                                addListenerToButton(node);
                            }} else {{
                                var newButtons = node.querySelectorAll(buttonSelector + ', ' + canvasCopyButtonSelector);
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

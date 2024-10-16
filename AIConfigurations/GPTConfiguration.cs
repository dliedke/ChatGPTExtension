/* *******************************************************************************************************************
 * Application: ChatGPTExtension
 * 
 * Autor:  Daniel Liedke
 * 
 * Copyright © Daniel Liedke 2024
 * Usage and reproduction in any manner whatsoever without the written permission of Daniel Liedke is strictly forbidden.
 *  
 * Purpose: All integration code for Open AI Chat GPT
 *           
 * *******************************************************************************************************************/

using System;
using System.Linq;

using Newtonsoft.Json;

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
    public const string GPT_COPY_CODE_BUTTON_SELECTOR = "button.flex.gap-1.items-center";
    public const string GPT_COPY_CODE_BUTTON_ICON_SELECTOR = "button.flex.gap-1.items-center svg.icon-sm";
    public const string GPT_CANVAS_COPY_BUTTON_SELECTOR = "button.h-10.rounded-lg.px-2.text-token-text-secondary";
    
    public string GetSetPromptScript(string promptText)
    {
        var escapedPrompt = JsonConvert.SerializeObject(promptText)
            .Trim('"')
            .Replace("'", "\\'")
            .Split(new[] { "\\r\\n", "\\n" }, StringSplitOptions.None)
            .Select(line => $"<p>{line}</p>")
            .Aggregate((current, next) => current + next);

        return $@"
                var promptArea = document.querySelector('div[id=""{GPT_PROMPT_TEXT_AREA_ID}""]');
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
        return @"
        document.querySelector('button svg path[d=""M9 7C9 4.23858 11.2386 2 14 2C16.7614 2 19 4.23858 19 7V15C19 18.866 15.866 22 12 22C8.13401 22 5 18.866 5 15V9C5 8.44772 5.44772 8 6 8C6.55228 8 7 8.44772 7 9V15C7 17.7614 9.23858 20 12 20C14.7614 20 17 17.7614 17 15V7C17 5.34315 15.6569 4 14 4C12.3431 4 11 5.34315 11 7V15C11 15.5523 11.4477 16 12 16C12.5523 16 13 15.5523 13 15V9C13 8.44772 13.4477 8 14 8C14.5523 8 15 8.44772 15 9V15C15 16.6569 13.6569 18 12 18C10.3431 18 9 16.6569 9 15V7Z""]')?.closest('button')?.click();";
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
        var buttonSelector = '{GPT_COPY_CODE_BUTTON_SELECTOR}';
        var iconSelector = '{GPT_COPY_CODE_BUTTON_ICON_SELECTOR}';
        var canvasCopyButtonSelector = '{GPT_CANVAS_COPY_BUTTON_SELECTOR}';

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
            var editor = document.querySelector('div[id=""{GPT_PROMPT_TEXT_AREA_ID}""]');
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
        var editor = document.querySelector('div[id=""{GPT_PROMPT_TEXT_AREA_ID}""]');
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
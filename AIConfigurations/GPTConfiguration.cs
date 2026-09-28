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
        public const string GPT_COPY_CODE_BUTTON_SELECTOR = "button:has(svg path[d^=\"M15.1006 1.78516\"])";
        public const string GPT_COPY_CODE_BUTTON_ICON_SELECTOR = "svg path[d^=\"M15.1006 1.78516\"]";
        // The composer no longer carries id="prompt-textarea"; it is a plain ProseMirror editor.
        public const string GPT_PROMPT_EDITOR_SELECTOR = "[data-composer-input-layout] div.ProseMirror[contenteditable=\"true\"]";
        public const string GPT_PROMPT_EDITOR_FALLBACK_SELECTOR = "div.ProseMirror[contenteditable=\"true\"]";

        // JS expression that returns the prompt editor: the legacy id first, then the new composer editor.
        public static string PromptAreaExpression =>
            $"(document.getElementById('{EscapeJs(AIConfiguration.GPTPromptTextAreaId)}')" +
            $" || document.querySelector('{EscapeJs(GPT_PROMPT_EDITOR_SELECTOR)}')" +
            $" || document.querySelector('{EscapeJs(GPT_PROMPT_EDITOR_FALLBACK_SELECTOR)}'))";

        // Escapes a value for use inside a single-quoted JS string literal.
        private static string EscapeJs(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'");
        }
        public const string GPT_CANVAS_COPY_BUTTON_SELECTOR = "button.no-draggable.text-token-text-primary";
        public const string GPT_BLACKBOARD_COPY_BUTTON_SELECTOR = "button.no-draggable.text-token-text-primary";

        public string GetSetPromptScript(string promptText)
        {
            // JS-safe, double-quoted string literal of the raw prompt text (newlines escaped as \n).
            var jsText = JsonConvert.SerializeObject(promptText);

            // HTML-encoded, <p>-wrapped variant used only by the legacy innerHTML fallback below.
            var escapedPrompt = JsonConvert.SerializeObject(System.Net.WebUtility.HtmlEncode(promptText))
                .Trim('"')
                .Replace("'", "\\'")
                .Split(new[] { "\\r\\n", "\\n" }, StringSplitOptions.None)
                .Select(line => $"<p>{line}</p>")
                .Aggregate((current, next) => current + next);

            // The composer is a ProseMirror editor. Text is inserted through real editing events so
            // ProseMirror records it in its document state and swaps the voice button for Send:
            //   1. execCommand('insertText') - genuine beforeinput/input events.
            //   2. A simulated paste (ClipboardEvent + DataTransfer).
            //   3. Legacy innerHTML injection (last resort).
            return $@"
                (function() {{
                    var promptArea = {PromptAreaExpression};
                    if (!promptArea) {{
                        console.error('GPT prompt area not found');
                        return;
                    }}
                    promptArea.focus();

                    var hasContent = promptArea.textContent && promptArea.textContent.trim().length > 0;
                    var textToInsert = (hasContent ? '\n\n' : '') + {jsText};

                    // Move the caret to the end of the editor before inserting.
                    try {{
                        var sel = window.getSelection();
                        sel.selectAllChildren(promptArea);
                        sel.collapseToEnd();
                    }} catch (e) {{}}

                    function grew(before) {{
                        return (promptArea.textContent ? promptArea.textContent.length : 0) > before;
                    }}

                    var beforeLen = promptArea.textContent ? promptArea.textContent.length : 0;

                    try {{ document.execCommand('insertText', false, textToInsert); }} catch (e) {{}}

                    if (!grew(beforeLen)) {{
                        try {{
                            var dt = new DataTransfer();
                            dt.setData('text/plain', textToInsert);
                            promptArea.dispatchEvent(new ClipboardEvent('paste', {{
                                clipboardData: dt,
                                bubbles: true,
                                cancelable: true
                            }}));
                        }} catch (e) {{}}
                    }}

                    if (!grew(beforeLen)) {{
                        var existingText = promptArea.innerHTML;
                        promptArea.innerHTML = existingText + (existingText && existingText.trim() ? '<p><br></p>' : '') + '{escapedPrompt}';
                        promptArea.dispatchEvent(new Event('input', {{ 'bubbles': true, 'cancelable': true }}));
                    }}
                }})();";
        }

        public string GetSubmitPromptScript()
        {
            // The Send button only appears once the composer has text (until then the primary slot
            // holds the 'Start Voice' button), and React renders it a tick or two after the text is
            // inserted by the previous ExecuteScriptAsync call. So we POLL for up to ~3s.
            //
            // Button detection order:
            //   1. data-testid='send-button' / id='composer-submit-button' (classic UI).
            //   2. The composer primary action button (bg-composer-primary), skipping the voice
            //      button, which is recognized by its waveform icon path (language independent).
            // If no enabled button appears in time, fall back to pressing Enter inside the editor.
            return $@"
                (function() {{
                    var VOICE_ICON = 'svg path[d^=""M8.22266 2.45825""]';

                    function findButton() {{
                        var b = document.querySelector('button[data-testid=""send-button""]') || document.getElementById('composer-submit-button');
                        if (b) {{ return b; }}

                        var primary = document.querySelectorAll('button[class*=""bg-composer-primary""]');
                        for (var i = primary.length - 1; i >= 0; i--) {{
                            if (!primary[i].querySelector(VOICE_ICON)) {{ return primary[i]; }}
                        }}
                        return null;
                    }}

                    function enabled(btn) {{
                        return btn && !btn.disabled && btn.getAttribute('aria-disabled') !== 'true';
                    }}

                    var attempts = 0;
                    var maxAttempts = 40; // 40 * 75ms = 3s
                    var timer = setInterval(function() {{
                        attempts++;
                        var btn = findButton();
                        if (enabled(btn)) {{
                            clearInterval(timer);
                            btn.click();
                            return;
                        }}
                        if (attempts >= maxAttempts) {{
                            clearInterval(timer);
                            var editor = {PromptAreaExpression};
                            if (editor) {{
                                editor.focus();
                                var keyOpts = {{ key: 'Enter', code: 'Enter', keyCode: 13, which: 13, bubbles: true, cancelable: true }};
                                ['keydown', 'keypress', 'keyup'].forEach(function(t) {{
                                    editor.dispatchEvent(new KeyboardEvent(t, keyOpts));
                                }});
                            }}
                        }}
                    }}, 75);
                }})();";
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
            // The code block copy button is now an icon-only button (aria-label='Copy', no text,
            // no data-state). It is matched by its copy icon path, which is language independent,
            // with the English aria-label as a fallback.
            //
            // The old 'button.flex.gap-1.items-center.select-none[data-state=closed]' selector must
            // not be used anymore: it now matches composer buttons ('Add files and more', the model
            // picker), so clicking them pasted the clipboard into VS. Composer and whole-message
            // copy buttons are excluded explicitly in case a cached remote config still carries it.
            //
            // This script is re-executed every 2s by a timer, so the MutationObserver is installed
            // once per page. postCopy() throttles the message so one click is reported once.
            return $@"
(function() {{
    var buttonSelector = '{EscapeJs(AIConfiguration.GPTCopyCodeButtonSelector)}';
    var iconSelector = '{EscapeJs(AIConfiguration.GPTCopyCodeButtonIconSelector)}';
    var canvasCopyButtonSelector = '{EscapeJs(AIConfiguration.GPTCanvasCopyButtonSelector)}';
    var blackboardCopyButtonSelector = '{EscapeJs(AIConfiguration.GPTBlackboardCopyButtonSelector)}';
    var newCanvasSelector = 'button.no-draggable.text-token-text-primary';
    var copyIconButtonSelector = '{EscapeJs(GPT_COPY_CODE_BUTTON_SELECTOR)}';
    var copyLabelSelector = 'button[aria-label=""Copy""], button[aria-label=""Copy code""]';
    var buttonSelectors = [buttonSelector, canvasCopyButtonSelector, blackboardCopyButtonSelector, newCanvasSelector, copyIconButtonSelector, copyLabelSelector];

    function isExcluded(button) {{
        return button.matches('[data-testid=""copy-turn-action-button""], [data-composer-navigation-target]')
            || !!button.closest('[data-composer-footer-responsive]');
    }}

    function postCopy() {{
        var now = Date.now();
        if (window.__gptExtLastCopyPost && (now - window.__gptExtLastCopyPost) < 700) {{ return; }}
        window.__gptExtLastCopyPost = now;
        window.chrome.webview.postMessage('CopyCodeButtonClicked');
    }}

    function addListenerToButton(button) {{
        if (!button || button.hasAttribute('data-listener-added') || isExcluded(button)) {{ return; }}
        button.addEventListener('click', function() {{ postCopy(); }}, true);
        button.setAttribute('data-listener-added', 'true');
    }}

    // Each selector is queried on its own so one invalid selector from the remote config
    // cannot disable the others.
    function scan(root) {{
        if (!root || !root.querySelectorAll) {{ return; }}
        buttonSelectors.forEach(function(selector) {{
            if (!selector) {{ return; }}
            try {{
                if (root.matches && root.matches(selector)) {{ addListenerToButton(root); }}
                root.querySelectorAll(selector).forEach(addListenerToButton);
            }} catch (e) {{}}
        }});
        try {{
            root.querySelectorAll(iconSelector).forEach(function(icon) {{
                addListenerToButton(icon.closest('button'));
            }});
        }} catch (e) {{}}
    }}

    scan(document);

    if (!window.__gptExtCopyObserver && document.body) {{
        window.__gptExtCopyObserver = new MutationObserver(function(mutations) {{
            mutations.forEach(function(mutation) {{
                if (mutation.type !== 'childList') {{ return; }}
                mutation.addedNodes.forEach(function(node) {{
                    if (node.nodeType === Node.ELEMENT_NODE) {{ scan(node); }}
                }});
            }});
        }});
        window.__gptExtCopyObserver.observe(document.body, {{ childList: true, subtree: true }});
    }}
}})();";
        }

        public string GetHomeKeyScript(bool shiftPressed, int startOfLine)
        {
            return $@"
        (function() {{
            var editor = {PromptAreaExpression};
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
        var editor = {PromptAreaExpression};
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

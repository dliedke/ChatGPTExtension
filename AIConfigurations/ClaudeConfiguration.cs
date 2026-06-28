/* *******************************************************************************************************************
 * Application: Chat GPT Extension
 * 
 * Autor:  Daniel Liedke
 * 
 * Copyright � Daniel Liedke 2025
 * Usage and reproduction in any manner whatsoever without the written permission of Daniel Liedke is strictly forbidden.
 *  
 * Purpose: All integration code for Claude AI
 *           
 * *******************************************************************************************************************/

using System;
using System.Linq;

using Newtonsoft.Json;

namespace ChatGPTExtension
{

    public class ClaudeConfiguration
    {
        #region Singleton

        private static ClaudeConfiguration _instance;
        private static readonly object _lock = new object();

        public static ClaudeConfiguration Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new ClaudeConfiguration();
                        }
                    }
                }
                return _instance;
            }
        }

        private ClaudeConfiguration() { }

        #endregion

        // Constants
        public const string CLAUDE_URL = "https://claude.ai/chats";
        public const string CLAUDE_PROMPT_CLASS = "ProseMirror";
        public const string CLAUDE_COPY_CODE_BUTTON_TEXT = "Copy";
        public const string CLAUDE_PROJECT_COPY_CODE_BUTTON_SELECTOR = "button.inline-flex[data-state=\"closed\"] svg path[d=\"M200,32H163.74a47.92,47.92,0,0,0-71.48,0H56A16,16,0,0,0,40,48V216a16,16,0,0,0,16,16H200a16,16,0,0,0,16-16V48A16,16,0,0,0,200,32Zm-72,0a32,32,0,0,1,32,32H96A32,32,0,0,1,128,32Zm72,184H56V48H82.75A47.93,47.93,0,0,0,80,64v8a8,8,0,0,0,8,8h80a8,8,0,0,0,8-8V64a47.93,47.93,0,0,0-2.75-16H200Z\"]";
        public const string CLAUDE_ARTIFACT_COPY_BUTTON_SELECTOR = "div[role=\"menuitem\"][data-orientation=\"vertical\"].font-base.py-1\\.5.px-2.rounded-lg.cursor-pointer:not([data-testid=\"delete-chat-trigger\"])";

        public string GetSetPromptScript(string promptText)
        {
            // JS-safe, double-quoted string literal of the raw prompt text (newlines escaped as \n).
            var jsText = JsonConvert.SerializeObject(promptText);

            // HTML-encoded, <p>-wrapped variant used only by the legacy innerHTML fallback below.
            var escapedHtml = JsonConvert.SerializeObject(System.Net.WebUtility.HtmlEncode(promptText))
                .Trim('"')
                .Replace("'", "\\'")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Split(new[] { "\\r\\n", "\\n" }, StringSplitOptions.None)
                .Select(line => $"<p>{line}</p>")
                .Aggregate((current, next) => current + next);

            // The editor is ProseMirror (React-controlled). Writing innerHTML directly makes the text
            // appear visually but leaves the editor 'empty' from the framework's point of view, so the
            // Send button never actually submits. We must insert through real editing events so
            // ProseMirror records the text in its internal document state. We try, in order:
            //   1. execCommand('insertText') - generates genuine beforeinput/input events (most reliable).
            //   2. A simulated paste (ClipboardEvent + DataTransfer).
            //   3. Legacy innerHTML injection (last resort; may not enable Send).
            return $@"
                (function() {{
                    var element = document.querySelector('.{AIConfiguration.ClaudePromptClass}');
                    if (!element) return;
                    element.focus();

                    var hasContent = element.textContent && element.textContent.trim().length > 0;
                    var textToInsert = (hasContent ? '\n\n' : '') + {jsText};

                    // Move the caret to the end of the editor before inserting.
                    try {{
                        var sel = window.getSelection();
                        sel.selectAllChildren(element);
                        sel.collapseToEnd();
                    }} catch (e) {{}}

                    function grew(before) {{
                        return (element.textContent ? element.textContent.length : 0) > before;
                    }}

                    var beforeLen = element.textContent ? element.textContent.length : 0;

                    // 1. execCommand insertText - real input events that ProseMirror observes.
                    try {{ document.execCommand('insertText', false, textToInsert); }} catch (e) {{}}

                    // 2. Simulated paste.
                    if (!grew(beforeLen)) {{
                        try {{
                            var dt = new DataTransfer();
                            dt.setData('text/plain', textToInsert);
                            element.dispatchEvent(new ClipboardEvent('paste', {{
                                clipboardData: dt,
                                bubbles: true,
                                cancelable: true
                            }}));
                        }} catch (e) {{}}
                    }}

                    // 3. Legacy innerHTML injection (only if nothing above worked).
                    if (!grew(beforeLen)) {{
                        var existingHtml = element.innerHTML;
                        element.innerHTML = existingHtml + (existingHtml && existingHtml.trim() ? '<p><br></p>' : '') + '{escapedHtml}';
                        element.dispatchEvent(new Event('input', {{ 'bubbles': true, 'cancelable': true }}));
                    }}
                }})();";
        }

        public string GetSubmitPromptScript()
        {
            // The send button is now an icon-font button (a <span data-cds=""Icon""> instead of an SVG).
            //
            // TIMING: the prompt text is inserted by a separate ExecuteScriptAsync call immediately
            // before this one. ProseMirror/React enables the Send button asynchronously (a tick or two
            // later), so clicking right away hits a still-disabled button and nothing is sent. Instead
            // of a fixed delay we POLL for an enabled send button for up to ~3s, then dispatch a full
            // pointer/mouse event sequence (a plain .click() does not always trigger Claude's handler).
            //
            // Button detection order, most to least specific:
            //   1. aria-label (covers the English UI directly).
            //   2. The primary colored ""fill/claude"" styled icon button that contains the CDS icon
            //      span. Language independent; scanned from the end of the document so we hit the
            //      composer send button rather than other styled buttons (dialogs, upgrade, etc.).
            //   3. The legacy SVG up-arrow selector, kept for older UI versions.
            // If no enabled button appears in time, fall back to pressing Enter inside the editor.
            return @"
                (function() {
                    function fire(btn) {
                        if (!btn) { return false; }
                        var opts = { bubbles: true, cancelable: true, view: window };
                        var types = ['pointerdown', 'mousedown', 'pointerup', 'mouseup', 'click'];
                        for (var k = 0; k < types.length; k++) {
                            var t = types[k];
                            var ev;
                            try {
                                ev = (t.indexOf('pointer') === 0) ? new PointerEvent(t, opts) : new MouseEvent(t, opts);
                            } catch (e) {
                                try { ev = new MouseEvent(t === 'pointerdown' ? 'mousedown' : (t === 'pointerup' ? 'mouseup' : t), opts); } catch (e2) { ev = null; }
                            }
                            if (ev) { btn.dispatchEvent(ev); }
                        }
                        if (typeof btn.click === 'function') { btn.click(); }
                        return true;
                    }

                    function findButton() {
                        var b = document.querySelector('button[aria-label=""Send message""]');
                        if (b) { return b; }
                        var candidates = document.querySelectorAll('button[class*=""_fill_""][class*=""_claude_""]');
                        for (var i = candidates.length - 1; i >= 0; i--) {
                            if (candidates[i].querySelector('span[data-cds=""Icon""]')) { return candidates[i]; }
                        }
                        if (candidates.length) { return candidates[candidates.length - 1]; }
                        var svg = document.querySelector('svg[viewBox=""0 0 256 256""] path[d=""M208.49,120.49a12,12,0,0,1-17,0L140,69V216a12,12,0,0,1-24,0V69L64.49,120.49a12,12,0,0,1-17-17l72-72a12,12,0,0,1,17,0l72,72A12,12,0,0,1,208.49,120.49Z""]');
                        if (svg) { return svg.closest('button'); }
                        return null;
                    }

                    function enabled(btn) {
                        return btn && !btn.disabled && btn.getAttribute('aria-disabled') !== 'true';
                    }

                    var attempts = 0;
                    var maxAttempts = 40; // 40 * 75ms = 3s
                    var timer = setInterval(function() {
                        attempts++;
                        var btn = findButton();
                        if (enabled(btn)) {
                            fire(btn);
                            clearInterval(timer);
                            return;
                        }
                        if (attempts >= maxAttempts) {
                            clearInterval(timer);
                            // Fallback: press Enter inside the editor.
                            var editor = document.querySelector('.ProseMirror');
                            if (editor) {
                                editor.focus();
                                var keyOpts = { key: 'Enter', code: 'Enter', keyCode: 13, which: 13, bubbles: true, cancelable: true };
                                ['keydown', 'keypress', 'keyup'].forEach(function(t) {
                                    editor.dispatchEvent(new KeyboardEvent(t, keyOpts));
                                });
                            }
                        }
                    }, 75);
                })();";
        }

        public string GetAttachFileScript()
        {
            return @"document.querySelector('input[data-testid=""file-upload""]')?.click();";
        }

        public string GetIsFileAttachedScript()
        {
            return @"
        var result = 'notfound';
        var divs = document.querySelectorAll('div[data-testid]');
        for (var i = 0; i < divs.length; i++) {
            if (divs[i].getAttribute('data-testid').startsWith('GPTExtension_')) {
                result = 'found';
                break;
            }
        }
        result;";
        }

        public string GetAddEventListenersScript()
        {
            return @"
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

// Updated selector using SVG path starting with 'M12.5 3C13.3284' (language-independent copy icon)
var updatedButtons = document.querySelectorAll('button[data-state=""closed""] svg[viewBox=""0 0 20 20""] path[d^=""M12.5 3""]');
updatedButtons.forEach(function(path) {
    var button = path.closest('button[data-state=""closed""]');
    if (button) {
        addClickListener(button, 'CopyCodeButtonClicked');
    }
});

// Additional selector for the project copy code button (legacy)
var projectCopyButtons = document.querySelectorAll('button.flex.flex-row.items-center.gap-1\\.5.rounded-md[data-state=""closed""]');
projectCopyButtons.forEach(function(button) {
    var path = button.querySelector('svg path[d=""M200,32H163.74a47.92,47.92,0,0,0-71.48,0H56A16,16,0,0,0,40,48V216a16,16,0,0,0,16,16H200a16,16,0,0,0,16-16V48A16,16,0,0,0,200,32Zm-72,0a32,32,0,0,1,32,32H96A32,32,0,0,1,128,32Zm72,184H56V48H82.75A47.93,47.93,0,0,0,80,64v8a8,8,0,0,0,8,8h80a8,8,0,0,0,8-8V64a47.93,47.93,0,0,0-2.75-16H200Z""]');
    if (path) {
        addClickListener(button, 'CopyCodeButtonClicked');
    }
});

// Artifact copy buttons (rounded-l-lg style)
var borderCopyButtons = document.querySelectorAll('button.font-base-bold.\\!text-xs.rounded-l-lg.bg-bg-000');
borderCopyButtons.forEach(function(button) {
    addClickListener(button, 'CopyCodeButtonClicked');
});

// Artifact copy buttons (menuitem divs with specific attributes, excluding delete button)
var artifactCopyButtons = document.querySelectorAll('div[role=""menuitem""][data-orientation=""vertical""].font-base.py-1\\.5.px-2.rounded-lg.cursor-pointer:not([data-testid=""delete-chat-trigger""])');
artifactCopyButtons.forEach(function(menuItem) {
    addClickListener(menuItem, 'CopyCodeButtonClicked');
});

// Observer for dynamically added buttons
var observer = new MutationObserver(function(mutations) {
    mutations.forEach(function(mutation) {
        if (mutation.type === 'childList') {
            mutation.addedNodes.forEach(function(node) {
                if (node.nodeType === Node.ELEMENT_NODE) {
                    // Check for all button types
                    var newUpdatedButtons = node.querySelectorAll ? node.querySelectorAll('button[data-state=""closed""] svg[viewBox=""0 0 20 20""] path[d^=""M12.5 3""]') : [];
                    newUpdatedButtons.forEach(function(path) {
                        var button = path.closest('button[data-state=""closed""]');
                        if (button) {
                            addClickListener(button, 'CopyCodeButtonClicked');
                        }
                    });

                    var newProjectCopyButtons = node.querySelectorAll ? node.querySelectorAll('button.flex.flex-row.items-center.gap-1\\.5.rounded-md[data-state=""closed""]') : [];
                    newProjectCopyButtons.forEach(function(button) {
                        var path = button.querySelector('svg path[d=""M200,32H163.74a47.92,47.92,0,0,0-71.48,0H56A16,16,0,0,0,40,48V216a16,16,0,0,0,16,16H200a16,16,0,0,0,16-16V48A16,16,0,0,0,200,32Zm-72,0a32,32,0,0,1,32,32H96A32,32,0,0,1,128,32Zm72,184H56V48H82.75A47.93,47.93,0,0,0,80,64v8a8,8,0,0,0,8,8h80a8,8,0,0,0,8-8V64a47.93,47.93,0,0,0-2.75-16H200Z""]');
                        if (path) {
                            addClickListener(button, 'CopyCodeButtonClicked');
                        }
                    });

                    var newBorderCopyButtons = node.querySelectorAll ? node.querySelectorAll('button.font-base-bold.\\!text-xs.rounded-l-lg.bg-bg-000') : [];
                    newBorderCopyButtons.forEach(function(button) {
                        addClickListener(button, 'CopyCodeButtonClicked');
                    });

                    var newArtifactCopyButtons = node.querySelectorAll ? node.querySelectorAll('div[role=""menuitem""][data-orientation=""vertical""].font-base.py-1\\.5.px-2.rounded-lg.cursor-pointer:not([data-testid=""delete-chat-trigger""])') : [];
                    newArtifactCopyButtons.forEach(function(menuItem) {
                        addClickListener(menuItem, 'CopyCodeButtonClicked');
                    });
                }
            });
        }
    });
});

observer.observe(document.body, { childList: true, subtree: true });";
        }


        public string GetHomeEndKeyScript(string key, bool shiftPressed)
        {
            return $@"
        (function() {{
            var editor = document.querySelector('.{AIConfiguration.ClaudePromptClass}');
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
    }
}
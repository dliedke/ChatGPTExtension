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
            // Claude's CDS redesign turned the send button into an icon-font button
            // (<button data-cds='Button'><span data-cds='Icon'>) with a localized aria-label,
            // so the button is now located by its stable data-testid='chat-input-send'.
            //
            // TIMING: the prompt text is inserted by a separate ExecuteScriptAsync call immediately
            // before this one. ProseMirror/React enables the Send button asynchronously (a tick or two
            // later), so clicking right away hits a still-disabled button and nothing is sent. Instead
            // of a fixed delay we POLL for an enabled send button for up to ~3s, then dispatch a full
            // pointer/mouse event sequence (a plain .click() does not always trigger Claude's handler).
            //
            // Button detection order, most to least specific:
            //   1. data-testid='chat-input-send' (current UI, language independent).
            //   2. aria-label (English UI, older builds).
            //   3. The brand-colored composer button: a CDS button whose inner background span uses
            //      'bg-fill-brand'. Language independent; scanned from the end of the document so we
            //      hit the composer send button rather than other brand buttons (dialogs, upgrade...).
            //   4. The legacy '_fill_'/'_claude_' styled button and the legacy SVG up-arrow.
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
                        var b = document.querySelector('button[data-testid=""chat-input-send""]');
                        if (b) { return b; }
                        b = document.querySelector('button[aria-label=""Send message""]');
                        if (b) { return b; }

                        var cds = document.querySelectorAll('button[data-cds=""Button""]');
                        for (var i = cds.length - 1; i >= 0; i--) {
                            if (cds[i].querySelector('span[class*=""bg-fill-brand""]')) { return cds[i]; }
                        }

                        var candidates = document.querySelectorAll('button[class*=""_fill_""][class*=""_claude_""]');
                        for (var j = candidates.length - 1; j >= 0; j--) {
                            if (candidates[j].querySelector('span[data-cds=""Icon""]')) { return candidates[j]; }
                        }
                        if (candidates.length) { return candidates[candidates.length - 1]; }

                        var svg = document.querySelector('svg[viewBox=""0 0 256 256""] path[d=""M208.49,120.49a12,12,0,0,1-17,0L140,69V216a12,12,0,0,1-24,0V69L64.49,120.49a12,12,0,0,1-17-17l72-72a12,12,0,0,1,17,0l72,72A12,12,0,0,1,208.49,120.49Z""]');
                        if (svg) { return svg.closest('button'); }
                        return null;
                    }

                    function enabled(btn) {
                        // The CDS button reports its disabled state through aria-disabled / data-disabled
                        // as well as the native disabled property. data-trigger-disabled is a tooltip
                        // attribute and must NOT be treated as a disabled button.
                        return btn
                            && !btn.disabled
                            && btn.getAttribute('aria-disabled') !== 'true'
                            && !btn.hasAttribute('data-disabled');
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
            // Claude's CDS redesign renders every action as an icon-font button
            // (<button data-cds='Button'><span data-cds='Icon'>). The old SVG paths and the
            // data-state='closed' markup are gone and the icon glyph carries no text, so the
            // only hint left on the element is the LOCALIZED aria-label. Detection therefore
            // works in three layers:
            //   1. Legacy selectors (older Claude UIs) - kept so old builds keep working.
            //   2. New CDS buttons whose aria-label/title looks like 'copy' in the languages
            //      Claude's UI is offered in (e.g. 'Copy to clipboard', 'Copiar para a area
            //      de transferencia').
            //   3. A clipboard hook: navigator.clipboard.writeText/write and
            //      document.execCommand('copy') report a copy when they run right after the
            //      user clicked a button. This is language- and markup-independent and covers
            //      code block, artifact and project copy buttons in any future redesign. It is
            //      gated on a recent button click so page-driven copies are ignored.
            // postCopy() throttles the message so layers 2 and 3 cannot report the same click twice.
            return @"
(function() {
    var COPY_LABEL = /cop(y|ie|ia|iar|ier)|kopi|\u043a\u043e\u043f\u0438\u0440|\u0646\u0633\u062e|\u30b3\u30d4\u30fc|\u590d\u5236|\u8907\u88fd|\ubcf5\uc0ac/i;

    function postCopy() {
        var now = Date.now();
        if (window.__gptExtLastCopyPost && (now - window.__gptExtLastCopyPost) < 700) { return; }
        window.__gptExtLastCopyPost = now;
        window.chrome.webview.postMessage('CopyCodeButtonClicked');
    }

    function addClickListener(element) {
        if (!element || element.hasAttribute('data-listener-added')) { return; }
        element.addEventListener('click', function() { postCopy(); }, true);
        element.setAttribute('data-listener-added', 'true');
    }

    function isCopyLabel(element) {
        var label = element.getAttribute('aria-label') || element.getAttribute('title') || '';
        return COPY_LABEL.test(label);
    }

    function scan(root) {
        if (!root || !root.querySelectorAll) { return; }

        // Legacy: code block copy button (language-independent copy icon path).
        root.querySelectorAll('button[data-state=""closed""] svg[viewBox=""0 0 20 20""] path[d^=""M12.5 3""]').forEach(function(path) {
            addClickListener(path.closest('button[data-state=""closed""]'));
        });

        // Legacy: project copy code button.
        root.querySelectorAll('button.flex.flex-row.items-center.gap-1\\.5.rounded-md[data-state=""closed""]').forEach(function(button) {
            if (button.querySelector('svg path[d=""M200,32H163.74a47.92,47.92,0,0,0-71.48,0H56A16,16,0,0,0,40,48V216a16,16,0,0,0,16,16H200a16,16,0,0,0,16-16V48A16,16,0,0,0,200,32Zm-72,0a32,32,0,0,1,32,32H96A32,32,0,0,1,128,32Zm72,184H56V48H82.75A47.93,47.93,0,0,0,80,64v8a8,8,0,0,0,8,8h80a8,8,0,0,0,8-8V64a47.93,47.93,0,0,0-2.75-16H200Z""]')) {
                addClickListener(button);
            }
        });

        // Legacy: artifact copy buttons (rounded-l-lg style).
        root.querySelectorAll('button.font-base-bold.\\!text-xs.rounded-l-lg.bg-bg-000').forEach(addClickListener);

        // Legacy: artifact copy buttons (menuitem divs, excluding the delete entry).
        root.querySelectorAll('div[role=""menuitem""][data-orientation=""vertical""].font-base.py-1\\.5.px-2.rounded-lg.cursor-pointer:not([data-testid=""delete-chat-trigger""])').forEach(addClickListener);

        // Current UI: CDS icon buttons identified by their localized copy label.
        root.querySelectorAll('button[data-cds=""Button""]').forEach(function(button) {
            if (isCopyLabel(button)) { addClickListener(button); }
        });
    }

    scan(document);

    // This script is re-executed periodically, so the observer and the clipboard hook are
    // installed only once per page to avoid stacking duplicates.
    if (!window.__gptExtCopyObserver && document.body) {
        window.__gptExtCopyObserver = new MutationObserver(function(mutations) {
            mutations.forEach(function(mutation) {
                if (mutation.type !== 'childList') { return; }
                mutation.addedNodes.forEach(function(node) {
                    if (node.nodeType !== Node.ELEMENT_NODE) { return; }
                    if (node.matches && node.matches('button[data-cds=""Button""]') && isCopyLabel(node)) {
                        addClickListener(node);
                    }
                    scan(node);
                });
            });
        });
        window.__gptExtCopyObserver.observe(document.body, { childList: true, subtree: true });
    }

    if (!window.__gptExtClipboardHooked) {
        window.__gptExtClipboardHooked = true;

        document.addEventListener('click', function(e) {
            var target = e.target;
            var trigger = (target && target.closest) ? target.closest('button, [role=""button""], [role=""menuitem""]') : null;
            window.__gptExtLastButtonClick = trigger ? Date.now() : 0;
        }, true);

        var clickedRecently = function() {
            return window.__gptExtLastButtonClick && (Date.now() - window.__gptExtLastButtonClick) < 1500;
        };

        if (navigator.clipboard) {
            var originalWriteText = navigator.clipboard.writeText;
            if (typeof originalWriteText === 'function') {
                navigator.clipboard.writeText = function() {
                    if (clickedRecently()) { postCopy(); }
                    return originalWriteText.apply(navigator.clipboard, arguments);
                };
            }
            var originalWrite = navigator.clipboard.write;
            if (typeof originalWrite === 'function') {
                navigator.clipboard.write = function() {
                    if (clickedRecently()) { postCopy(); }
                    return originalWrite.apply(navigator.clipboard, arguments);
                };
            }
        }

        var originalExecCommand = document.execCommand;
        document.execCommand = function(command) {
            if (command && String(command).toLowerCase() === 'copy' && clickedRecently()) { postCopy(); }
            return originalExecCommand.apply(document, arguments);
        };
    }
})();";
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
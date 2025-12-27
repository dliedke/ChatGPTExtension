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
            // First encode the HTML characters
            var htmlEncoded = System.Net.WebUtility.HtmlEncode(promptText);

            // Then do the JSON serialization and other processing
            var escapedCode = JsonConvert.SerializeObject(htmlEncoded)
                .Trim('"')
                .Replace("'", "\\'")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Split(new[] { "\\r\\n", "\\n" }, StringSplitOptions.None)
                .Select(line => $"<p>{line}</p>")
                .Aggregate((current, next) => current + next);

            return $@"
                var element = document.querySelector('.{AIConfiguration.ClaudePromptClass}');
                var existingHtml = element.innerHTML;
                var newContent = existingHtml + (existingHtml && existingHtml.trim() ? '<p><br></p>' : '') + '{escapedCode}';
                element.innerHTML = newContent;
                var inputEvent = new Event('input', {{
                    'bubbles': true,
                    'cancelable': true
                }});
                element.dispatchEvent(inputEvent);";
        }

        public string GetSubmitPromptScript()
        {
            return @"var svg = document.querySelector('svg[viewBox=""0 0 256 256""] path[d=""M208.49,120.49a12,12,0,0,1-17,0L140,69V216a12,12,0,0,1-24,0V69L64.49,120.49a12,12,0,0,1-17-17l72-72a12,12,0,0,1,17,0l72,72A12,12,0,0,1,208.49,120.49Z""]');
                    if (svg) {
                        svg.closest('button').click();
                    }";
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
var updatedButtons = document.querySelectorAll('button[data-state=""closed""] svg[viewBox=""0 0 20 20""] path[d^=""M12.5 3C13.3284""]');
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
                    var newUpdatedButtons = node.querySelectorAll ? node.querySelectorAll('button[data-state=""closed""] svg[viewBox=""0 0 20 20""] path[d^=""M12.5 3C13.3284""]') : [];
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
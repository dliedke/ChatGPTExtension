/* *******************************************************************************************************************
 * Application: ChatGPTExtension
 * 
 * Autor:  Daniel Liedke
 * 
 * Copyright © Daniel Liedke 2024
 * Usage and reproduction in any manner whatsoever without the written permission of Daniel Liedke is strictly forbidden.
 *  
 * Purpose: All integration code for Claude AI
 *           
 * *******************************************************************************************************************/

using System;
using System.Linq;

using Newtonsoft.Json;

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
    private const string CLAUDE_PROJECT_COPY_CODE_BUTTON_SELECTOR = "button.inline-flex[data-state=\"closed\"] svg path[d=\"M200,32H163.74a47.92,47.92,0,0,0-71.48,0H56A16,16,0,0,0,40,48V216a16,16,0,0,0,16,16H200a16,16,0,0,0,16-16V48A16,16,0,0,0,200,32Zm-72,0a32,32,0,0,1,32,32H96A32,32,0,0,1,128,32Zm72,184H56V48H82.75A47.93,47.93,0,0,0,80,64v8a8,8,0,0,0,8,8h80a8,8,0,0,0,8-8V64a47.93,47.93,0,0,0-2.75-16H200Z\"]";

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
                var element = document.querySelector('.{CLAUDE_PROMPT_CLASS}');
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
        return @"
        document.querySelector('[aria-label=""Send Message""]').click();
        document.querySelector('button.w-full.flex.items-center.bg-bg-200').click();";
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
        return $@"
        function addClickListener(button, message)
        {{
            if (!button.hasAttribute('data-listener-added'))
            {{
                button.addEventListener('click', function() {{
                    window.chrome.webview.postMessage(message);
                }});
                button.setAttribute('data-listener-added', 'true');
            }}
        }}

        var allButtons = Array.from(document.querySelectorAll('button'));
        var copyCodeButtons = allButtons.filter(function(button) {{
            var spanElement = button.querySelector('span');
            return spanElement && spanElement.textContent.trim() === '{CLAUDE_COPY_CODE_BUTTON_TEXT}';
        }});

        copyCodeButtons.forEach(function(button) {{
            addClickListener(button, 'CopyCodeButtonClicked');
        }});

        var newButton = document.querySelector('button.inline-flex[data-state=""closed""][class*=""rounded-md""][class*=""h-8""][class*=""w-8""]');
        if (newButton)
        {{
            addClickListener(newButton, 'CopyCodeButtonClicked');
        }}

        var newButtonSvg = document.querySelector('{CLAUDE_PROJECT_COPY_CODE_BUTTON_SELECTOR}');
        if (newButtonSvg)
        {{
            var newButton = newButtonSvg.closest('button');
            if (newButton)
            {{
                addClickListener(newButton, 'CopyCodeButtonClicked');
            }}
        }}";
    }


    public string GetHomeEndKeyScript(string key, bool shiftPressed)
    {
        return $@"
        (function() {{
            var editor = document.querySelector('.{CLAUDE_PROMPT_CLASS}');
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
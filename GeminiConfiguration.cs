/* *******************************************************************************************************************
 * Application: ChatGPTExtension
 * 
 * Autor:  Daniel Liedke
 * 
 * Copyright © Daniel Liedke 2024
 * Usage and reproduction in any manner whatsoever without the written permission of Daniel Liedke is strictly forbidden.
 *  
 * Purpose: All integration code for Google Gemini AI
 *           
 * *******************************************************************************************************************/

using System;
using System.Linq;

using Newtonsoft.Json;

public class GeminiConfiguration
{
    private static GeminiConfiguration _instance;
    private static readonly object _lock = new object();

    public static GeminiConfiguration Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new GeminiConfiguration();
                    }
                }
            }
            return _instance;
        }
    }

    private GeminiConfiguration() { }

    // Constants
    public const string GEMINI_URL = "https://gemini.google.com";
    public const string GEMINI_PROMPT_CLASS = "ql-editor";
    public const string GEMINI_COPY_CODE_BUTTON_CLASS = "copy-button";

    public string GetHomeEndKeyScript(string key, bool shiftPressed)
    {
        return $@"
        (function() {{
            var editor = document.querySelector('.{GEMINI_PROMPT_CLASS}');
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

    public string GetSetPromptScript(string promptText)
    {
        var lines = promptText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var codeHtml = string.Join("", lines.Select(line => $"<p>{line}</p>"));
        var codeHtmlJson = JsonConvert.SerializeObject(codeHtml);

        return $@"
                (function() {{
                    if (!window.myTrustedTypesPolicy) {{
                        window.myTrustedTypesPolicy = trustedTypes.createPolicy('default', {{
                            createHTML: (string) => string
                        }});
                    }}
                    const trustedHTML = window.myTrustedTypesPolicy.createHTML({codeHtmlJson});
                    var element = document.querySelector('.{GEMINI_PROMPT_CLASS}');
                    var existingHtml = element.innerHTML;
                    var newContent = existingHtml + (existingHtml && existingHtml.trim() ? '<p><br></p>' : '') + trustedHTML;
                    element.innerHTML = newContent;
                    var inputEvent = new Event('input', {{
                        'bubbles': true,
                        'cancelable': true
                    }});
                    element.dispatchEvent(inputEvent);
                }})();";
    }

    public string GetSubmitPromptScript()
    {
        return @"
        var sendButton = document.querySelector('button.send-button[mat-icon-button]');
        if (sendButton) {
            sendButton.click();
        } else {
            console.error('Send button not found');
        }";
    }

    public string GetAddEventListenersScript()
    {
        return $@"
        var allButtons = Array.from(document.getElementsByClassName('{GEMINI_COPY_CODE_BUTTON_CLASS}'));
        var targetButtons = allButtons.filter(function(button) {{
            return !button.hasAttribute('data-listener-added');
        }});

        targetButtons.forEach(function(button) {{
            button.addEventListener('click', function() {{
                window.chrome.webview.postMessage('CopyCodeButtonClicked');
            }});
            button.setAttribute('data-listener-added', 'true');
        }});";
    }

    public string GetReceiveCodeScript(string selectedCode)
    {
        var lines = selectedCode.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var codeHtml = string.Join("", lines.Select(line => $"<p>{line}</p>"));
        var codeHtmlJson = JsonConvert.SerializeObject(codeHtml);

        return $@"
        (function() {{
            if (!window.myTrustedTypesPolicy) {{
                window.myTrustedTypesPolicy = trustedTypes.createPolicy('default', {{
                    createHTML: (string) => string
                }});
            }}
            const trustedHTML = window.myTrustedTypesPolicy.createHTML({codeHtmlJson});
            var element = document.querySelector('.{GEMINI_PROMPT_CLASS}');
            element.innerHTML = element.innerHTML + trustedHTML;
            var inputEvent = new Event('input', {{
                'bubbles': true,
                'cancelable': true
            }});
            element.dispatchEvent(inputEvent);
        }})();";
    }
}
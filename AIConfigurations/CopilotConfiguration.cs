/* *******************************************************************************************************************
 * Application: ChatGPTExtension
 * 
 * Autor:  Daniel Liedke
 * 
 * Copyright © Daniel Liedke 2024
 * Usage and reproduction in any manner whatsoever without the written permission of Daniel Liedke is strictly forbidden.
 *  
 * Purpose: All integration code for Microsoft Enterprise Copilot
 *           
 * *******************************************************************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;

public class CopilotConfiguration
{
    #region Singleton

    private static CopilotConfiguration _instance;
    private static readonly object _lock = new object();

    public static CopilotConfiguration Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new CopilotConfiguration();
                    }
                }
            }
            return _instance;
        }
    }

    private CopilotConfiguration() { }

    #endregion

    // Constants
    public const string COPILOT_ENTERPRISE_URL = "https://copilot.microsoft.com/?FORM=undexpand&";
    public const string COPILOT_PROMPT_SELECTOR = "#cib-action-bar-main /deep/ #searchbox";
    public const string COPILOT_COPY_CODE_BUTTON_SELECTOR = "div.code-header > div > button";

    // Updated selectors for Copilot Enterprise
    public const string COPILOT_SEND_BUTTON_SELECTOR = "#cib-action-bar-main /deep/ div.bottom-right-controls > div > button";
    public const string COPILOT_ATTACH_FILE_BUTTON_SELECTOR = "#cib-action-bar-main /deep/ #file-upload-container > button";

    // Headers needed for enterprise authentication
    public static readonly Dictionary<string, string> EnterpriseHeaders = new Dictionary<string, string>
    {
        { "ms-domain-claim", "msft" },
        { "ms-user-claims", "tenant" },
        { "ms-client-type", "enterprise" },
        { "ms-client-location", "chat" }
    };

    public static async Task ConfigureWebView(CoreWebView2 webView2)
    {
        // Add enterprise-specific script
        await webView2.ExecuteScriptAsync(@"
            // Set enterprise flags
            window.localStorage.setItem('isEnterprise', 'true');
            window.localStorage.setItem('enterpriseAllowed', 'true');
            
            // Add enterprise class to body
            document.body.classList.add('enterprise-mode');
        ");
    }

    public string GetSetPromptScript(string promptText)
    {
        var escapedPrompt = JsonConvert.SerializeObject(promptText)
            .Trim('"')
            .Replace("'", "\\'")
            .Split(new[] { "\\r\\n", "\\n" }, StringSplitOptions.None)
            .Select(line => line.Replace("\\\"", "\""))
            .Aggregate((current, next) => current + "\\n" + next);

        return $@"
        (function() {{
            const searchbox = document.querySelector('{COPILOT_PROMPT_SELECTOR}');
            if (searchbox) {{
                searchbox.value = '{escapedPrompt}';
                searchbox.dispatchEvent(new Event('input', {{ bubbles: true }}));
            }} else {{
                console.error('Copilot prompt area not found');
            }}
        }})();";
    }

    public string GetSubmitPromptScript()
    {
        return $@"
        (function() {{
            const sendButton = document.querySelector('{COPILOT_SEND_BUTTON_SELECTOR}');
            if (sendButton) {{
                sendButton.click();
            }} else {{
                console.error('Copilot send button not found');
            }}
        }})();";
    }

    public string GetAttachFileScript()
    {
        return $@"
        (function() {{
            const attachButton = document.querySelector('{COPILOT_ATTACH_FILE_BUTTON_SELECTOR}');
            if (attachButton) {{
                attachButton.click();
            }} else {{
                console.error('Copilot attach file button not found');
            }}
        }})();";
    }

    public string GetIsFileAttachedScript()
    {
        return @"
        var result = 'notfound';
        var fileUploads = document.querySelectorAll('div[role=""list""] span');
        for (var i = 0; i < fileUploads.length; i++) {
            if (fileUploads[i].textContent.startsWith('GPTExtension_')) {
                result = 'found';
                break;
            }
        }
        result;";
    }

    public string GetAddEventListenersScript()
    {
        return $@"
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
                button.setAttribute('data-listener-added', 'true');
            }}
        }}

        // Initial button setup
        var copyButtons = document.querySelectorAll('{COPILOT_COPY_CODE_BUTTON_SELECTOR}');
        copyButtons.forEach(addListenerToButton);

        // Set up observer for dynamically added buttons
        var observer = new MutationObserver(function(mutations) {{
            mutations.forEach(function(mutation) {{
                if (mutation.type === 'childList') {{
                    mutation.addedNodes.forEach(function(node) {{
                        if (node.nodeType === Node.ELEMENT_NODE) {{
                            var newButtons = node.querySelectorAll('{COPILOT_COPY_CODE_BUTTON_SELECTOR}');
                            newButtons.forEach(addListenerToButton);
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
            const searchbox = document.querySelector('{COPILOT_PROMPT_SELECTOR}');
            if (searchbox) {{
                const text = searchbox.value;
                const cursorPos = searchbox.selectionStart;
                let newPos;

                if ('{key}' === 'Home') {{
                    // Find start of current line
                    newPos = text.lastIndexOf('\n', cursorPos - 1) + 1;
                    if (newPos === 0) newPos = 0;
                }} else if ('{key}' === 'End') {{
                    // Find end of current line
                    newPos = text.indexOf('\n', cursorPos);
                    if (newPos === -1) newPos = text.length;
                }}

                if ({(shiftPressed ? "true" : "false")}) {{
                    searchbox.setSelectionRange(Math.min(searchbox.selectionStart, newPos), 
                                             Math.max(searchbox.selectionEnd, newPos));
                }} else {{
                    searchbox.setSelectionRange(newPos, newPos);
                }}
            }}
        }})();";
    }
}

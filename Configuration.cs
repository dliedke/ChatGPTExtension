/* *******************************************************************************************************************
 * Application: Chat GPT Extension
 * 
 * Autor:  Daniel Liedke
 * 
 * Copyright © Daniel Liedke 2025
 * Usage and reproduction in any manner whatsoever without the written permission of Daniel Liedke is strictly forbidden.
 *  
 * Purpose: Store default model to use in extension GPT or Gemini
 *           
 * *******************************************************************************************************************/

namespace ChatGPTExtension
{
    public class Configuration
    {
        public int GptConfigured { get; set; } = 1; // Default to GPT
        public bool ButtonsAtTop { get; set; } = false; // Default to bottom
        public bool EnableCopyCode { get; set; } = true; // Default to enabled

        // Proxy Configuration
        public bool UseProxy { get; set; } = false;
        public string ProxyServer { get; set; } = "";
        public int ProxyPort { get; set; } = 8080;
        public bool ProxyRequiresAuth { get; set; } = false;
        public string ProxyUsername { get; set; } = "";
        public string ProxyPassword { get; set; } = "";
        public bool UseSystemProxy { get; set; } = false;
        public string ProxyBypassList { get; set; } = "";
    }
}

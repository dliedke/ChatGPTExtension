﻿/* *******************************************************************************************************************
 * Application: ChatGPTExtension
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
    }
}

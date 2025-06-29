/* *******************************************************************************************************************
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
        public int GptConfigured { get; set; }

        public ButtonNames ButtonNames { get; set; } = new ButtonNames();
    }

    public class ButtonNames
    {
        public string VSNETToGPT { get; set; } = "VS.NET to {aiTechnology} ➡️";
        public string FixCodeInGPT { get; set; } = "Fix Code in {aiTechnology} ➡️";
        public string ImproveCodeInGPT { get; set; } = "Improve Code in {aiTechnology} ➡️";
        public string GPTToVSNET { get; set; } = "⬅️ {aiTechnology} to VS.NET";
        public string ContinueCode { get; set; } = "Continue Code in {aiTechnology} ⏩";
        public string CompleteCode { get; set; } = "Complete Code in {aiTechnology} ✅";
        public string NewFile { get; set; } = "📄 New File";
        public string AttachFile { get; set; } = "Attach Open File to {aiTechnology}📎";
    }
}

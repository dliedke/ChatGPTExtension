using Newtonsoft.Json;
using System;
using System.IO;

namespace ChatGPTExtension
{
    public class ButtonLabelsConfiguration
    {
        public string VSNETToAI { get; set; } = "Editor to {AI}";
        public string FixCode { get; set; } = "Fix Code";
        public string FixCodePrompt { get; set; } = "Fix {languageCode} code below:";

        public string ImproveCode { get; set; } = "Improve Code";
        public string ImproveCodePrompt { get; set; } = "Improve {languageCode} code below:";

        public string CompleteCode { get; set; } = "Complete Code";
        public string CompleteCodePrompt { get; set; } = "Please show new full complete code without explanations with complete methods implementation for the provided code without any placeholders like ... or assuming code segments. Do not create methods you dont know. Keep all original comments.";

        public string ContinueCode { get; set; } = "Continue Code";
        public string ContinueCodePrompt { get; set; } = "Continue code generation";

        public string AIToVSNET { get; set; } = "{AI} to Editor";

        public string NewFile { get; set; } = "📄 New File";

        public string AttachFile { get; set; } = "📎Attach File";

        public string EnableCopyCode { get; set; } = "Copy Code";

        private const string FileName = "buttonlabels.json";

        private static readonly string _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ChatGPTExtension", FileName);

        public static ButtonLabelsConfiguration Load()
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    var json = File.ReadAllText(_configPath);
                    return JsonConvert.DeserializeObject<ButtonLabelsConfiguration>(json) ?? new ButtonLabelsConfiguration();
                }
                catch
                {
                    return new ButtonLabelsConfiguration();
                }
            }
            return new ButtonLabelsConfiguration();
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath));
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(_configPath, json);
        }
    }
}

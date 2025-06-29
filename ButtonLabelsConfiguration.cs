using System;
using System.IO;
using Newtonsoft.Json;

namespace ChatGPTExtension
{
    public class ButtonLabelsConfiguration
    {
        public string VSNETToAI { get; set; } = "VS.NET to {AI} ➡️";
        public string FixCode { get; set; } = "Fix Code in {AI} ➡️";
        public string ImproveCode { get; set; } = "Improve Code in {AI} ➡️";
        public string AIToVSNET { get; set; } = "⬅️ {AI} to VS.NET";
        public string ContinueCode { get; set; } = "Continue Code ⏩";
        public string CompleteCode { get; set; } = "Complete Code ✅";
        public string NewFile { get; set; } = "📄 New File";
        public string AttachFile { get; set; } = "Attach Current VS File📎";
        public string EnableCopyCode { get; set; } = "Enable Copy Code";

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

using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ChatGPTExtension
{
    public class AIConfiguration
    {
        public GPTSettings GPT { get; set; }
        public GeminiSettings Gemini { get; set; }
        public ClaudeSettings Claude { get; set; }
        public DateTime LastUpdated { get; set; }

        // Static holders for current configuration
        private static string _gptUrl = GPTConfiguration.CHAT_GPT_URL;
        private static string _gptPromptTextAreaId = GPTConfiguration.GPT_PROMPT_TEXT_AREA_ID;
        private static string _gptCopyCodeButtonSelector = GPTConfiguration.GPT_COPY_CODE_BUTTON_SELECTOR;
        private static string _gptCopyCodeButtonIconSelector = GPTConfiguration.GPT_COPY_CODE_BUTTON_ICON_SELECTOR;
        private static string _gptCanvasCopyButtonSelector = GPTConfiguration.GPT_CANVAS_COPY_BUTTON_SELECTOR;

        private static string _geminiUrl = GeminiConfiguration.GEMINI_URL;
        private static string _geminiPromptClass = GeminiConfiguration.GEMINI_PROMPT_CLASS;
        private static string _geminiCopyCodeButtonClass = GeminiConfiguration.GEMINI_COPY_CODE_BUTTON_CLASS;

        private static string _claudeUrl = ClaudeConfiguration.CLAUDE_URL;
        private static string _claudePromptClass = ClaudeConfiguration.CLAUDE_PROMPT_CLASS;
        private static string _claudeCopyCodeButtonText = ClaudeConfiguration.CLAUDE_COPY_CODE_BUTTON_TEXT;
        private static string _claudeProjectCopyCodeButtonSelector = ClaudeConfiguration.CLAUDE_PROJECT_COPY_CODE_BUTTON_SELECTOR;

        // Public static getters
        public static string GPTUrl => _gptUrl;
        public static string GPTPromptTextAreaId => _gptPromptTextAreaId;
        public static string GPTCopyCodeButtonSelector => _gptCopyCodeButtonSelector;
        public static string GPTCopyCodeButtonIconSelector => _gptCopyCodeButtonIconSelector;
        public static string GPTCanvasCopyButtonSelector => _gptCanvasCopyButtonSelector;

        public static string GeminiUrl => _geminiUrl;
        public static string GeminiPromptClass => _geminiPromptClass;
        public static string GeminiCopyCodeButtonClass => _geminiCopyCodeButtonClass;

        public static string ClaudeUrl => _claudeUrl;
        public static string ClaudePromptClass => _claudePromptClass;
        public static string ClaudeCopyCodeButtonText => _claudeCopyCodeButtonText;
        public static string ClaudeProjectCopyCodeButtonSelector => _claudeProjectCopyCodeButtonSelector;

        // Method to update static values from current configuration
        internal void UpdateStaticValues()
        {
            if (GPT != null)
            {
                _gptUrl = GPT.Url ?? _gptUrl;
                _gptPromptTextAreaId = GPT.PromptTextAreaId ?? _gptPromptTextAreaId;
                _gptCopyCodeButtonSelector = GPT.CopyCodeButtonSelector ?? _gptCopyCodeButtonSelector;
                _gptCopyCodeButtonIconSelector = GPT.CopyCodeButtonIconSelector ?? _gptCopyCodeButtonIconSelector;
                _gptCanvasCopyButtonSelector = GPT.CanvasCopyButtonSelector ?? _gptCanvasCopyButtonSelector;
            }

            if (Gemini != null)
            {
                _geminiUrl = Gemini.Url ?? _geminiUrl;
                _geminiPromptClass = Gemini.PromptClass ?? _geminiPromptClass;
                _geminiCopyCodeButtonClass = Gemini.CopyCodeButtonClass ?? _geminiCopyCodeButtonClass;
            }

            if (Claude != null)
            {
                _claudeUrl = Claude.Url ?? _claudeUrl;
                _claudePromptClass = Claude.PromptClass ?? _claudePromptClass;
                _claudeCopyCodeButtonText = Claude.CopyCodeButtonText ?? _claudeCopyCodeButtonText;
                _claudeProjectCopyCodeButtonSelector = Claude.ProjectCopyCodeButtonSelector ?? _claudeProjectCopyCodeButtonSelector;
            }
        }

        public class GPTSettings
        {
            public string Url { get; set; }
            public string PromptTextAreaId { get; set; }
            public string CopyCodeButtonSelector { get; set; }
            public string CopyCodeButtonIconSelector { get; set; }
            public string CanvasCopyButtonSelector { get; set; }
        }

        public class GeminiSettings
        {
            public string Url { get; set; }
            public string PromptClass { get; set; }
            public string CopyCodeButtonClass { get; set; }
        }

        public class ClaudeSettings
        {
            public string Url { get; set; }
            public string PromptClass { get; set; }
            public string CopyCodeButtonText { get; set; }
            public string ProjectCopyCodeButtonSelector { get; set; }
        }

        public class AIConfigurationManager
        {
            private const string GITHUB_CONFIG_URL = "https://raw.githubusercontent.com/dliedke/ChatGPTExtension/refs/heads/master/ai-config.json";
            private const string LOCAL_CACHE_FILENAME = "ai-config-cache.json";
            private const int CACHE_DURATION_HOURS = 24;
            private static readonly string LOCAL_CACHE_PATH = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ChatGPTExtension",
                LOCAL_CACHE_FILENAME
            );

            private static AIConfigurationManager _instance;
            private static readonly object _lock = new object();
            private AIConfiguration _currentConfig;

            public static AIConfigurationManager Instance
            {
                get
                {
                    if (_instance == null)
                    {
                        lock (_lock)
                        {
                            if (_instance == null)
                            {
                                _instance = new AIConfigurationManager();
                            }
                        }
                    }
                    return _instance;
                }
            }

            private AIConfigurationManager()
            {
                // Initialize with default values
                _currentConfig = GetDefaultConfiguration();
            }

            public async Task InitializeAsync()
            {
                var config = await LoadConfigurationAsync();
                config.UpdateStaticValues();
            }

            private async Task<AIConfiguration> LoadConfigurationAsync()
            {
                try
                {
                    // Try to load from cache first
                    var cachedConfig = LoadFromCache();

                    // Check if we need to refresh the cache
                    if (_currentConfig == null || ShouldRefreshCache(cachedConfig.LastUpdated))
                    {
                        // Get config from cache if it is still valid
                        if (cachedConfig != null && !ShouldRefreshCache(cachedConfig.LastUpdated))
                        {
                            _currentConfig = cachedConfig;
                            return _currentConfig;
                        }

                        // Try to fetch from GitHub
                        var githubConfig = await FetchFromGitHubAsync();
                        if (githubConfig != null)
                        {
                            _currentConfig = githubConfig;
                            SaveToCache(_currentConfig);
                            return _currentConfig;
                        }
                    }

                    return _currentConfig ?? GetDefaultConfiguration();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in GetConfigurationAsync: {ex.Message}");
                    return GetDefaultConfiguration();
                }
            }

            private bool ShouldRefreshCache(DateTime? lastUpdated = null)
            {
                // Check if cache file exists
                if (!File.Exists(LOCAL_CACHE_PATH))
                {
                    return true;  // First time installation, should refresh cache
                }

                // Use provided lastUpdated or get it from current config
                DateTime checkDate = lastUpdated ??
                    (_currentConfig != null ? _currentConfig.LastUpdated : DateTime.UtcNow);

                return (DateTime.UtcNow - checkDate).TotalHours >= CACHE_DURATION_HOURS;
            }

            private AIConfiguration LoadFromCache()
            {
                try
                {
                    if (File.Exists(LOCAL_CACHE_PATH))
                    {
                        var json = File.ReadAllText(LOCAL_CACHE_PATH);
                        return JsonConvert.DeserializeObject<AIConfiguration>(json);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading cache: {ex.Message}");
                }
                return null;
            }

            private void SaveToCache(AIConfiguration config)
            {
                try
                {
                    var directory = Path.GetDirectoryName(LOCAL_CACHE_PATH);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                    File.WriteAllText(LOCAL_CACHE_PATH, json);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error saving cache: {ex.Message}");
                }
            }

            private async Task<AIConfiguration> FetchFromGitHubAsync()
            {
                try
                {
                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("User-Agent", "ChatGPTExtension");
                        var response = await client.GetStringAsync(GITHUB_CONFIG_URL);
                        var config = JsonConvert.DeserializeObject<AIConfiguration>(response);
                        config.LastUpdated = DateTime.UtcNow;
                        return config;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error fetching from GitHub: {ex.Message}");
                    return null;
                }
            }

            private AIConfiguration GetDefaultConfiguration()
            {
                return new AIConfiguration
                {
                    LastUpdated = DateTime.UtcNow,
                    GPT = new GPTSettings
                    {
                        Url = GPTConfiguration.CHAT_GPT_URL,
                        PromptTextAreaId = GPTConfiguration.GPT_PROMPT_TEXT_AREA_ID,
                        CopyCodeButtonSelector = GPTConfiguration.GPT_COPY_CODE_BUTTON_SELECTOR,
                        CopyCodeButtonIconSelector = GPTConfiguration.GPT_COPY_CODE_BUTTON_ICON_SELECTOR,
                        CanvasCopyButtonSelector = GPTConfiguration.GPT_CANVAS_COPY_BUTTON_SELECTOR
                    },
                    Gemini = new GeminiSettings
                    {
                        Url = GeminiConfiguration.GEMINI_URL,
                        PromptClass = GeminiConfiguration.GEMINI_PROMPT_CLASS,
                        CopyCodeButtonClass = GeminiConfiguration.GEMINI_COPY_CODE_BUTTON_CLASS
                    },
                    Claude = new ClaudeSettings
                    {
                        Url = ClaudeConfiguration.CLAUDE_URL,
                        PromptClass = ClaudeConfiguration.CLAUDE_PROMPT_CLASS,
                        CopyCodeButtonText = ClaudeConfiguration.CLAUDE_COPY_CODE_BUTTON_TEXT,
                        ProjectCopyCodeButtonSelector = ClaudeConfiguration.CLAUDE_PROJECT_COPY_CODE_BUTTON_SELECTOR
                    }
                };
            }
        }
    }
}
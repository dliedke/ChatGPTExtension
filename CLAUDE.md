# CLAUDE.md - Chat GPT Extension Project Guide

## Project Overview

**Chat GPT Extension** is a Visual Studio 2022/2026 VSIX extension that embeds AI chat interfaces (OpenAI ChatGPT, Google Gemini, Anthropic Claude, Hangzhou Deepseek) directly into the IDE using WebView2. It uses JavaScript injection to automate interactions with the AI web interfaces — no API tokens required.

- **Author:** Daniel Carvalho Liedke
- **License:** Proprietary (free to use, no cloning/modification/selling)
- **Current Version:** 9.1

## Tech Stack

- **Language:** C# 7.3 / .NET Framework 4.7.2
- **UI:** WPF (XAML)
- **Browser:** WebView2 (Chromium-based embedded browser)
- **IDE Integration:** Visual Studio SDK (AsyncPackage, tool windows, commands)
- **Serialization:** MessagePack, Newtonsoft.Json
- **Target:** Visual Studio 2022 (17.14+) and VS 2026, x64 only

## Build Instructions

Build the solution using MSBuild (Release configuration):

```bash
"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\amd64\MSBuild.exe" "C:\GitLab\ChatGPTExtension\ChatGPTExtension.sln" /p:Configuration=Release
```

Output: `bin/Release/ChatGPTExtension.vsix`

There are no unit tests in this project.

## Version Management (IMPORTANT)

**When making any code changes, ALL THREE version locations MUST be updated together:**

### 1. Assembly Version — `Properties/AssemblyInfo.cs`
```csharp
[assembly: AssemblyVersion("X.Y.0.0")]
[assembly: AssemblyFileVersion("X.Y.0.0")]
```

### 2. VSIX Manifest Version — `source.extension.vsixmanifest`
```xml
<Identity Id="ChatGPTExtension.b5d7cf4a-65ad-4f6b-8f5a-5d27c2a5d4e2" Version="X.Y" ... />
```

### 3. README Changelog — `README.md`
Add a new entry at the top of the "What's New" section (line ~85):
```markdown
- X.Y - Brief description of the change.
```

**Version format:** The project uses `Major.Minor` format (e.g., `9.0`, `9.1`). For AssemblyInfo, it becomes `Major.Minor.0.0` (e.g., `9.1.0.0`). Increment the minor version for each release. Increment the major version for significant feature additions.

## Project Structure

```
ChatGPTExtension/
├── AIConfigurations/              # Per-AI-model integration logic
│   ├── ClaudeConfiguration.cs     # Claude AI selectors and JS injection
│   ├── DeepSeekConfiguration.cs   # Deepseek selectors and JS injection
│   ├── GeminiConfiguration.cs     # Gemini selectors and JS injection
│   └── GPTConfiguration.cs        # ChatGPT selectors and JS injection
├── DragAndDrop/                   # Drag-and-drop reordering for custom actions
│   ├── DragAdorner.cs
│   ├── ListViewDragDropManager.cs
│   └── MouseUtilities.cs
├── Properties/
│   └── AssemblyInfo.cs            # Assembly version (update on changes)
├── Resources/
│   └── ChatGPTWindowCommand.png
├── StoreInfo/                     # VS Marketplace assets
├── Themes/                        # Theme resources (currently empty)
│
├── ChatGPTExtensionPackage.cs     # Main VSIX package entry point (singleton)
├── ChatGPTWindowControl.xaml      # Main extension UI (XAML)
├── ChatGPTWindowControl.xaml.cs   # Main code-behind (~84KB, core logic)
├── ChatGPTToolWindow.cs           # VS tool window wrapper
├── ChatGPTWindowCommand.cs        # VS command handler
├── AIConfigurationManager.cs      # Loads/switches AI model configs (~19KB)
├── Configuration.cs               # Persistent settings (model, proxy, etc.)
├── ButtonLabelsConfiguration.cs   # Configurable button label texts
├── ActionItem.cs                  # Custom action data model
│
├── ConfigurationWindow.xaml/.cs   # Custom actions configuration UI
├── ButtonsConfigWindow.xaml/.cs   # Button labels configuration UI
├── ProxyConfigurationWindow.xaml/.cs  # Proxy settings UI
├── GPTWideWindow.xaml/.cs         # Wide-format response window
├── InputDialog.xaml/.cs           # Generic input dialog
│
├── ai-config.json                 # AI model URL/selector config (24h GitHub cache)
├── gptwide.txt                    # JavaScript for wide-format responses
├── source.extension.vsixmanifest  # VSIX manifest (update version on changes)
├── ChatGPTExtensionPackage.vsct   # VS command table (menus/buttons)
├── ChatGPTExtension.csproj        # Project file
├── ChatGPTExtension.sln           # Solution file
├── README.md                      # Documentation (update changelog on changes)
└── LICENSE.txt                    # Proprietary license
```

## Key Architecture Concepts

### AI Configuration System
Each AI model has a dedicated configuration class in `AIConfigurations/` that implements model-specific:
- CSS selectors for prompt areas, copy buttons, send buttons
- JavaScript injection scripts for automating interactions
- URL patterns and DOM element identification

`AIConfigurationManager.cs` orchestrates loading/switching between models and merges remote config from `ai-config.json` (fetched from GitHub with 24h cache).

### WebView2 Integration
The extension embeds a Chromium browser (WebView2) that loads AI chat websites. All automation is done via JavaScript injection — finding DOM elements by selectors and simulating user interactions.

### Configuration Persistence
`Configuration.cs` handles saving/loading user settings (selected AI model, proxy config, button positions, copy code toggle state) to the local filesystem at `%LocalAppData%\ChatGPTExtension`.

### Custom Actions
Users can define custom AI prompts (up to 20) that appear as buttons. These are managed through `ConfigurationWindow.xaml` with drag-and-drop reordering support from `DragAndDrop/`.

## Key Files for Common Tasks

| Task | Primary File(s) |
|------|-----------------|
| Fix AI selector/button detection | `AIConfigurations/<Model>Configuration.cs`, `ai-config.json` |
| Modify main UI buttons/layout | `ChatGPTWindowControl.xaml`, `ChatGPTWindowControl.xaml.cs` |
| Add new AI model | Create new `AIConfigurations/<Model>Configuration.cs`, update `AIConfigurationManager.cs`, add to `ai-config.json` |
| Change menu structure | `ChatGPTExtensionPackage.vsct`, `ChatGPTWindowCommand.cs` |
| Fix WebView2 interaction issues | `ChatGPTWindowControl.xaml.cs` (JS injection methods) |
| Modify settings/persistence | `Configuration.cs` |
| Change VS version requirements | `source.extension.vsixmanifest` |

## Coding Conventions

- **Singleton pattern** used for `ChatGPTExtensionPackage` and AI configuration classes
- **Async/await** throughout (inherits from `AsyncPackage`)
- **No unit tests** — changes are validated through manual regression testing
- **JavaScript strings** are embedded in C# as string literals for WebView2 injection
- **CSS selectors** in AI configurations may break when AI providers update their UIs — these are the most frequent maintenance items
- WPF data binding with `INotifyPropertyChanged` for UI updates
- Keep the code-behind style consistent — this project uses code-behind rather than MVVM

## Common Pitfalls

- AI providers frequently change their DOM structure, breaking CSS selectors. Check `ai-config.json` and `AIConfigurations/` classes when copy/paste stops working.
- WebView2 data directory is at `%LocalAppData%\ChatGPTExtension` — deleting it clears login sessions and cached data.
- The extension only supports x64 (amd64) architecture.
- VS minimum version is 17.14 — do not lower this requirement.
- JavaScript injection must handle timing — DOM elements may not be ready immediately after page load.

## Git Workflow

- **Main branch:** `master`
- **Commit style:** Version number followed by a dash and description (e.g., `9.0 - Fixed "Reload GPT" menu...`)
- Always build and verify the VSIX before committing

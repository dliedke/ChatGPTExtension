# Chat GPT Extension

The Chat GPT extension loads the **OpenAI Chat GPT web and integrates it with Visual Studio 2022 17.14 or higher.** 

It works with Free/Plus/Pro versions of OpenAI Chat GPT web. **No OpenAI tokens are required.**

Also integrates with **Google Gemini** Free and Advanced.

Also integrates with **Anthropic Claude AI** Haiku and Sonnet.

Also integrates with **Hangzhou Deepseek**.

## Features

1. Send selected code from VS.NET to GPT/Gemini/Claude/Deepseek
2. Send selected code from GPT/Gemini/Claude/Deepseek to VS.NET
3. When the GPT/Gemini/Claude/Deepseek "Copy code" button is clicked, the code is sent automatically to VS.NET
4. Buttons to automate code fixing, improve code and asking GPT/Gemini/Claude/Deepseek to show complete code and continue code generation
5. Other code operations like commenting code, adding exception handling, creating unit tests, and several others
6. Configurable custom actions for GPT/Gemini/Claude/Deepseek to apply to the code; these can be added, removed, changed, or reordered as needed
7. Last AI model used will be saved for next sessions
8. GPT wide script support for better experience with two monitors
9. Attach current code file to GPT/Claude/Deepseek

## Demo

![GPT Extension Demo](https://i.ibb.co/4PB1q5s/Chat-GPT-Extension-Demo.gif)

## Install

1. Close VS.NET
2. Download and install the extension
3. Open VS.NET
4. **Go to the menu View -> Other Windows and select Chat GPT Extension**
5. Log into Chat GPT

## Uninstall

1. In VS.NET, go to extensions -> manage extensions, click on installed, select and remove the Chat GPT Extension
2. Close VS.NET to apply changes
3. Delete the directory %appdata%\\..\Local\ChatGPTExtension

## Usage

- Use CTRL+mouse scroll to zoom in or out in the extension for better layout of the web site.

- The "VS.NET to GPT/Gemini/Claude/Deepseek" button copies the selected code from VS.NET to the GPT/Gemini/Claude/Deepseek prompt area, preserving any existing data.

- The "Fix Code in GPT/Gemini/Claude/Deepseek" button copies the selected code from VS.NET to the GPT/Gemini/Claude/Deepseek prompt area and asks AI to fix it.

- The "Improve Code in GPT/Gemini/Claude/Deepseek" button copies the selected code from VS.NET to the GPT/Gemini/Claude/Deepseek prompt area and asks AI to improve it.

- The "GPT/Gemini/Claude/Deepseek to VS.NET" button copies the selected text from GPT/Gemini/Claude/Deepseek and sends it to VS.NET.

- The "Attach Current File" button will attach current code file open in VS.NET to Claude, GPT or Deepseek.
  Make sure to hit enter after file is added in dialog because of security constraints

- The "Complete Code" button asks GPT/Gemini/Claude/Deepseek to show full complete code when it is showing partial code only.

- The "New File" button will retrieve the selected code from the page and asks for filename to add a new file with code in the project.

- The "Continue Code" button will ask the GPT/Gemini/Claude/Deepseek to continue the code generation when code is too long for single answer.

- The "Enable Copy Code" switch, when enabled, automatically sends the code to VS.NET when the "Copy code" button in GPT/Gemini/Claude/Deepseek is clicked.

- The "Arrow" button opens a submenu with other code operations that GPT/Gemini/Claude/Deepseek can perform.

- The "Reload Chat GPT/Gemini/Claude/Deepseek..." submenu reloads GPT/Gemini/Claude/Deepseek in the internal browser.

- The "Configure extension..." submenu opens the screen to configure the code action operations.

- The "GPT wide" button inside "Configure extension..." submenu will allow setting the script for Wide GPT responses.

- The "Use GPT" submenu will configure the extension to use Open AI Chat GPT.

- The "Use Gemini" submenu will configure the extension to use Google Gemini.

- The "Use Claude" submenu will configure the extension to use Anthropic Claude AI.

- The "Use Deepseek" submenu will configure the extension to use Hangzhou Deepseek AI.


## What's New

- 8.6 - Fixed GPT Blackboard copy button and Claude artifact copy button

- 8.5 - Renamed to Chat GPT Extension

- 8.4 - Fixed home/end keys in Deepseek prompt area. Implemented hybrid approach for Gemini attach file functionality due to automation preventions

- 8.3 - Fixed tab switching issues
 
- 8.2 - Fixed Deepseek integration issues

- 8.1 - Proxy support. Fixed Deepseek and Claude integrations after hard work

- 7.9 - Mininum VS.NET 2022 version 17.14 required for this extension. New attempt to fix random initialization issues.
 
- 7.8 - Fix random error initializing extension after first VS.NET initialization after boot

- 7.7 - Fix Claude copy code

- 7.6 - Fix for "Error: We couldn't create the data directory #12". Also added "About..." dialog to show app version

- 7.5 - m1001111 feat: Make settings prompts more flexible and fix display errors
Prompt in Settings Button can be set line by line Prompt in Settings Extensions can be set line by line Fixed border display bug

- 7.4 - Fix attach file not working for GPT

- 7.3 - Fix issues with attach code button not displaying for Claude after Gemini selection. Removed word "Chat" from some titles

- 7.2 - With the great help of m1001111: Better AI model selector, configurable button texts and actions so you may localize the extension for your own native language. Internal changes for better debugging.

- 7.1 - Added Python project support for New File button and no extension when project type is not detected 

- 7.0 - With the help of m1001111, Codex and Claude: Dark/light theme support according to VS.NET. Buttons now in the bottom closer to mouse. Fixed GPT attach file menu. Configurable button texts.

- 6.3 - Fix by m1001111: If DeepSeek was enabled and then switched to GPT, Gemini, or Claude, the DeepSeek checkbox would still be selected.
 
- 6.2 - Fix user reported issue "BUG: Gemini, You paused this reply". Fixed enter key and auto send prompt not working with Claude

- 6.1 - Added Hangzhou Deepseek AI integration

- 6.0 - Fix integration with Claude

- 5.9 - Compiled as x64 to fix issues not loading in latest VS.NET 2022

- 5.8 - Fixed attach file in GPT

- 5.7 - Fix selectors local cache issues

- 5.6 - Javascript selectors now in config file with 24h cache sync with github project. Also fixed selector for copy code button for GPT-4o Canvas

- 5.5 - Fix user reported issue: Random pasting of clipboard contents into code when using ChatGPT #3 for other scenarios<br>

- 5.4 - Fix user reported issue: Random pasting of clipboard contents into code when using ChatGPT #3<br>
Fix issues sending code with HTML tags to AI models

- 5.3 - Huge refactor for maintainability, no new functionalities. Full regression test completed.

- 5.2 - Support for GPT Canvas copy code button

- 5.1 - Fixed Claude integration

- 5.0 - Fixed GPT and Claude integrations

- 4.8 - Fixed attach file in GPT

- 4.7 - Fixed send prompt in GPT and Gemini

- 4.6 - Removed code to hide extension in some scenarios

- 4.5 - Added Minimize option when undocked the extension.<br>
        It is good for two monitors usage if you need to reduce quickly the window and then restore later for use.<br>
        Position of the original and minimized windows are saved during the session<br>

- 4.4 - Added new "Continue Code" button to continue code generation. Fixed buttons tooltips.<br>
        Support for Projects in Claude (new copy code button in the AI).<br>

- 4.3 - Fix prompts not submitting automatically in Claude.<br> 
        Support for Artifacts in Claude (new copy code button in the AI).<br>
        Improved complete code prompt to keep original code comments.<br>

- 4.2 - New File button. Select code in the AI page and click new file to add the code in a new file in the existing project

- 4.1 - Fixed issues attaching file in GPT when %temp% is windows default value in appdata

- 4.0 - Removing old temporary files in attachment and also fix for attaching file not yet saved.<br>
        All extension actions now working with attached files using the button.

- 3.8 - Fix attach file in GPT

- 3.7 - Added "Attach Current File" new option.<br>
        Make sure to hit enter after file is added in dialog because of security constraints.<br>
        File is copied as txt to temp directory so any extension can be added (ex: cshtml that is not supported in AIs).<br>
        Fixed Enter key not sending prompt for GPT<br>

- 3.6 - Fix issues with copy code functionality for Claude AI

- 3.5 - Fix issues with copy code functionality

- 3.4 - Big release!<br>
        Updated Chat GPT url to https://chatgpt.com/ and fixed all integration issues.<br>
        Updated GPT wide script for newer version.<br>
        Fix issues with reload AI menu and copy code button handler.<br>
        Fix issues with Gemini double new lines when receiving code from VS.NET.<br>

- 3.3 - Added support for Anthropic Claude AI!! Click in the arrow down button in the top to change default AI model

- 3.2 - Added support for GPT wide script (disabled by default). This will allow wider AI responses and it good when using two monitors and extension is placed another monitor. Click in The "GPT wide" button inside "Configure extension..." to setup. Thanks Scott for the suggestion!

- 3.1 - Support for Gemini in other languages than English

- 3.0 - Gemini support!! Click in the arrow down button in the top to change default AI model

- 2.8 - Better code for Copy button handler to avoid breaking when GPT HTML changes

- 2.7 - Fix Copy Code button handler

- 2.6 - Fix AI prompt to add comments to the code

- 2.5 - Fix auto scroll to bottom for new GPT trained up to April 2023

- 2.4 - Fix Home, End, Shift-Home and Shift-End keys in GPT textbox prompt area.

- 2.3 - Automatically hiding WebView2 with GPT if tool windows from bottom of VS.NET are activated. For example, this will fix issues with GPT over Output window for example when autohide is active.

- 2.2 - Added new button "Complete Code" to ask GPT to show full complete code. Fixed issues with Shift-Home and Shift-End in GPT prompt textbox. Also added tooltips for help.

- 2.1 - Removed "Preview" from the extension.

- 2.0 - Added drag and drop support for reordering custom actions. Fixed issues detecting changes that need saving. A maximum of 20 custom actions can now be added.

- 1.7 - Custom actions can now be added, removed, edited, and reordered in a grid. The current GPT prompt will be cleared for custom actions to run. Note: Due to migration, existing customized actions from version 1.6 will be lost. We apologize for any inconvenience.

- 1.6 - Introduced configurable custom actions for GPT to apply to the code.

- 1.5 - GPT now receives the detected language name, when available, for better response accuracy.

- 1.4 - Fixed issues with the end and home keys inside the GPT prompt textbox. Added more actions to process code in the submenu.

- 1.3 - Initial release.

## Known Issues

There are some known random issues with logging off from Chat GPT. If you encounter problems logging off, try pressing F12 and then trying again, or close VS.NET and delete the directory %appdata%\\..\Local\ChatGPTExtension.

## Licensing

The extension is free to use but may not be cloned, modified, or sold in any manner.

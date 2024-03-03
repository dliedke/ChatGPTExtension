/* *******************************************************************************************************************
 * Application: ChatGPTExtension
 * 
 * Autor:  Daniel Liedke
 * 
 * Copyright © Daniel Liedke 2024
 * Usage and reproduction in any manner whatsoever without the written permission of Daniel Liedke is strictly forbidden.
 *  
 * Purpose: Window to configure GPT wide javascript
 *           
 * *******************************************************************************************************************/

using System;
using System.IO;
using System.Windows;

namespace ChatGPTExtension
{
    public partial class GPTWideWindow : Window
    {

        private const string _scriptFileName = "gptwide.txt";
        private static readonly string _appDataPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChatGPTExtension", "GPTWide");
        private static readonly string _fullScriptPath = System.IO.Path.Combine(_appDataPath, _scriptFileName);

        public GPTWideWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadScriptFromFile();
        }

        private void LoadScriptFromFile()
        {
			// Try to load configured script, if not found load default script
			// defined in this file
            string content = null;
            if (File.Exists(_fullScriptPath))
            {
                content = File.ReadAllText(_fullScriptPath);
            }
            else
            {
                content = _scriptGPTWide;
            }

            txtScriptGPTWide.Text = content;
        }

        public static string GetGPTWideScript()
        {
			// Retrieve the GPT wide script if we have it
            if (File.Exists(_fullScriptPath))
            {
                return File.ReadAllText(_fullScriptPath);
            }
            return string.Empty;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
			// Close window
            this.Close();
        }
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
			// Save script and close window
            var scriptContent = txtScriptGPTWide.Text;
            SaveScriptToFile(scriptContent);
            this.Close();
        }

        private void SaveScriptToFile(string content)
        {
            try
            {
				// Create directory if required
                if (!Directory.Exists(_appDataPath))
                {
                    Directory.CreateDirectory(_appDataPath);
                }

				// Save file and show message
                File.WriteAllText(_fullScriptPath, content);
                MessageBox.Show("Script saved successfully.\r\nReload GPT to apply changes.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save the script. Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

		// Open the link for the script
        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            // Use Process.Start to open the link in the default browser
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true // Necessary for .NET Core and .NET 5/6 applications
            });

            // Mark the event as handled
            e.Handled = true;
        }

        // Script from https://www.reddit.com/r/ChatGPT/comments/15nbpaa/chatgpts_webinterface_width_fix/
		//
        private string _scriptGPTWide = @"// ==UserScript==
// @name         ChatGPT CSS fixes
// @version      2024-02-11
// @updateURL    https://gist.github.com/alexchexes/d2ff0b9137aa3ac9de8b0448138125ce/raw/chatgpt_ui_fix.user.js
// @downloadURL  https://gist.github.com/alexchexes/d2ff0b9137aa3ac9de8b0448138125ce/raw/chatgpt_ui_fix.user.js
// @namespace    http://tampermonkey.net/
// @description  Adjusts width of side bar and messages of the chatGPT web interface
// @author       alexchexes
// @match        https://chat.openai.com/*
// @icon         https://www.google.com/s2/favicons?sz=64&domain=openai.com
// @grant        none
// ==/UserScript==

(function() {
	const accentColor = `#f39c12`;

	const messagesCss = `
		/* Message body width */
		@media (min-width: 1280px) {
			.xl\\:max-w-3xl {
				max-width: 90% !important;
			}
		}
		@media (min-width: 1024px) {
			.lg\\:max-w-\\[38rem\\] {
				max-width: 90% !important;
			}
		}
		@media (min-width: 768px) {
			.md\\:max-w-2xl {
				max-width: 90% !important;
			}
			.md\\:max-w-3xl {
				max-width: 90% !important;
			}
		}

		/* Code blocks font */
		code, pre {
			font-family: Consolas,Söhne Mono,Monaco,Andale Mono,Ubuntu Mono,monospace!important;
			/* font-family: Iosevka Custom, Söhne Mono,Monaco,Andale Mono,Ubuntu Mono,monospace!important; */
			/* font-size: 12px !important; */
		}

		/* Code blocks background color */
		pre > div.rounded-md {
			background-color: #1e1e1f;
		}

		/* Code blocks headings background color */
		pre > div.rounded-md > div.flex.items-center.relative {
			background-color: #424245;
		}


		/* Bring back background destinction between bot and user messages */
		/* DARK THEME */
		html.dark .flex.flex-col.pb-9.text-sm .w-full.text-token-text-primary[data-testid]:nth-child(odd) {
			background-color: #252527;
		}
		/* LIGHT THEME */
		html.light .flex.flex-col.pb-9.text-sm .w-full.text-token-text-primary[data-testid]:nth-child(odd) {
			background-color: #f2f2f2;
		}


		/* Make top bar transparent as it consumes vertical space for no reason */
		/* DARK THEME */
		html.dark div.sticky.top-0.flex.items-center.justify-between.z-10.h-14.p-2.font-semibold {
			background-color: rgba(52,53,65,0);
			background-image: linear-gradient(90deg, #0d0d0d 0%, transparent 20%);
		}
		/* LIGHT THEME */
		html.light div.sticky.top-0.flex.items-center.justify-between.z-10.h-14.p-2.font-semibold {
			background-color: rgba(52,53,65,0);
			background-image: linear-gradient(90deg, #fff 0%, transparent 20%);
		}

		/* Make GPT version number more visible */
		html.dark .group.flex.cursor-pointer.items-center.gap-1.rounded-xl.py-2.px-3.text-lg.font-medium.hover\\:bg-gray-50.radix-state-open\\:bg-gray-50.dark\\:hover\\:bg-black\\/10.dark\\:radix-state-open\\:bg-black\\/20 span.text-token-text-secondary {
			color: ${accentColor};
		}

		/* BREAK LINES IN CODE BLOCKS */
		code.\\!whitespace-pre {
			white-space: pre-wrap !important;
		}
	`;

	const sidebar_new_width = `330px`;

	const sidebar_container_selector = `.flex-shrink-0.overflow-x-hidden[style^=""width: 260px""]`;

	const sidebarCss = `
		/* Sidebar width */
		${sidebar_container_selector},
		${sidebar_container_selector} .w-\\[260px\\] {
			width: ${sidebar_new_width} !important;
		}

		/* Adjust position of the new show/hide-sidebar control button to match the new width */
		main div.fixed.left-0.top-1\\/2.z-40 {
			transform: translateX(0px) translateY(-50%) rotate(180deg) translateZ(0px) !important;
		}


		/*------------------*/
		/* Sidebar elements */
		/*------------------*/

		/* History periods headings color */
		html.dark h3.h-9.pb-2.pt-3.px-2.text-xs.font-medium.text-ellipsis.overflow-hidden.break-all.text-token-text-tertiary {
			color: ${accentColor};
		}

		/* Buttons on active chat (to make it visible when title is too long) */
		html.dark div.group.relative.rounded-lg.active\\:opacity-90.bg-token-sidebar-surface-secondary button.flex.items-center.justify-center.text-token-text-primary.transition.hover\\:text-token-text-secondary.radix-state-open\\:text-token-text-secondary svg > path {
			fill: ${accentColor};
		}

		ol > li > div > a > div.relative.grow.overflow-hidden.whitespace-nowrap {
			overflow: visible;
			white-space: unset;
		}

		ol > li > div > a > div.relative.grow.overflow-hidden.whitespace-nowrap > div.absolute.to-transparent {
			background-image: none;
		}

		a.hover\\:pr-4:hover,
		a.flex.py-3.px-3.items-center.gap-3.relative.rounded-md.hover\\:bg-gray-100.dark\\:hover\\:bg-\\[\\#2A2B32\\].cursor-pointer.break-all.bg-gray-50.hover\\:pr-4.dark\\:bg-gray-900.group
		{
			padding-right: unset !important;
		}

		div.absolute.inset-y-0.right-0.w-8.z-10.bg-gradient-to-l.dark\\:from-gray-900.from-gray-50.group-hover\\:from-gray-100.dark\\:group-hover\\:from-\\[\\#2A2B32\\] {
			background: none;
		}
	`;

	const cssStyles = (messagesCss + sidebarCss).replaceAll(""\t"", ' ');

	// Create a new <style> element and set its content to the CSS rules
	var styleElement = document.createElement(""style"");
	styleElement.innerHTML = cssStyles;

	// Append the new <style> element to the <head> section of the document
	document.head.appendChild(styleElement);
})();";

    }
}

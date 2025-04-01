/* *******************************************************************************************************************
 * Application: ChatGPTExtension
 * 
 * Autor:  Daniel Liedke
 * 
 * Copyright © Daniel Liedke 2025
 * Usage and reproduction in any manner whatsoever without the written permission of Daniel Liedke is strictly forbidden.
 *  
 * Purpose: Tool window exposed by this package and hosts a user control
 *           
 * *******************************************************************************************************************/

using System;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.Shell;

namespace ChatGPTExtension
{
    /// <summary>
    /// This class implements the tool window exposed by this package and hosts a user control.
    /// </summary>
    /// <remarks>
    /// In Visual Studio tool windows are composed of a frame (implemented by the shell) and a pane,
    /// usually implemented by the package implementer.
    /// <para>
    /// This class derives from the ToolWindowPane class provided from the MPF in order to use its
    /// implementation of the IVsUIElementPane interface.
    /// </para>
    /// </remarks>
    [Guid("ba58fdf3-7af7-4b60-b69b-40ac68d83b75")]
    public class ChatGPTToolWindow : ToolWindowPane
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ChatGPTToolWindow"/> class.
        /// </summary>
        public ChatGPTToolWindow() : base(null)
        {
            this.Caption = "Chat GPT Extension";

            // Pass this package (which is an IServiceProvider) to the user control.
            // Also the full ToolWindow as second parameter
            this.Content = new GptToolWindowControl(this, this);
        }

        public void MinimizeWindow()
        {
            if (this.Frame is System.Windows.Window window)
            {
                window.WindowState = System.Windows.WindowState.Minimized;
            }
        }
    }
}

using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;

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
            this.Content = new GptToolWindowControl(this);
        }
    }

}

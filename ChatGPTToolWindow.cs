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

            // This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
            // we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on
            // the object returned by the Content property.
            this.Content = new GptToolWindowControl();
        }
    }
}

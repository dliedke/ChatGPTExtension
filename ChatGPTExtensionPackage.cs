/* *******************************************************************************************************************
 * Application: ChatGPTExtension
 * 
 * Autor:  Daniel Liedke
 * 
 * Copyright © Daniel Liedke 2025
 * Usage and reproduction in any manner whatsoever without the written permission of Daniel Liedke is strictly forbidden.
 *  
 * Purpose: Main Package class for the VS.NET extension
 *           
 * *******************************************************************************************************************/

using System;
using System.Threading;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

using Task = System.Threading.Tasks.Task;
using System.Diagnostics;
using System.IO;

namespace ChatGPTExtension
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(ChatGPTExtensionPackage.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(ChatGPTToolWindow), Style = VsDockStyle.Tabbed, Window = ToolWindowGuids80.SolutionExplorer)]

    public sealed class ChatGPTExtensionPackage : AsyncPackage
    {
        /// <summary>
        /// ChatGPTExtensionPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "4a77af14-c2b0-479a-a35a-056175d22b9c";

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await ChatGPTWindowCommand.InitializeAsync(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Clean up temporary files when the extension is being disposed
                CleanupUserFiles();
            }

            base.Dispose(disposing);
        }

        private void CleanupUserFiles()
        {
            try
            {
                string userDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string[] userFiles = Directory.GetFiles(userDirectory, "GPTExtension_*.txt");

                foreach (string userFile in userFiles)
                {
                    File.Delete(userFile);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in CleanupUserFiles(): {ex.Message}");
            }
        }

        #endregion
    }
}

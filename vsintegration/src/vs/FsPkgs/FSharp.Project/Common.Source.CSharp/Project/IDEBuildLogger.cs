// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Diagnostics;
using System.Globalization;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.Win32;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using Microsoft.VisualStudio.FSharp.LanguageService;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{

	/// <summary>
	/// This class implements an MSBuild logger that output events to VS outputwindow and tasklist.
	/// </summary>
	[ComVisible(true)]
	public sealed class IDEBuildLogger : Logger
	{
		#region fields
		// TODO: Remove these constants when we have a version that suppoerts getting the verbosity using automation.
#if FX_ATLEAST_45
		private string buildVerbosityRegistryRoot = @"Software\Microsoft\VisualStudio\12.0";
#else
		private string buildVerbosityRegistryRoot = @"Software\Microsoft\VisualStudio\10.0";
#endif
		private const string buildVerbosityRegistrySubKey = @"General";
		private const string buildVerbosityRegistryKey = "MSBuildLoggerVerbosity";
		// TODO: Re-enable this constants when we have a version that suppoerts getting the verbosity using automation.
		//private const string EnvironmentCategory = "Environment";
		//private const string ProjectsAndSolutionSubCategory = "ProjectsAndSolution";
		//private const string BuildAndRunPage = "BuildAndRun";

		private int currentIndent;
		private IVsOutputWindowPane outputWindowPane;
		private string errorString = SR.GetString(SR.Error, CultureInfo.CurrentUICulture);
		private string warningString = SR.GetString(SR.Warning, CultureInfo.CurrentUICulture);
		private bool isLogTaskDone;
		private TaskProvider taskProvider;
		private IVsHierarchy hierarchy;
		private IServiceProvider serviceProvider;
        private TaskReporter taskReporter;
        private bool haveCachedRegistry = false;

		#endregion

		#region properties
		public string WarningString
		{
			get { return this.warningString; }
			set { this.warningString = value; }
		}
		public string ErrorString
		{
			get { return this.errorString; }
			set { this.errorString = value; }
		}
		public bool IsLogTaskDone
		{
			get { return this.isLogTaskDone; }
			set { this.isLogTaskDone = value; }
		}
		/// <summary>
		/// When building from within VS, setting this will
		/// enable the logger to retrive the verbosity from
		/// the correct registry hive.
		/// </summary>
		/*internal, but public for FSharp.Project.dll*/ public string BuildVerbosityRegistryRoot
		{
			get { return buildVerbosityRegistryRoot; }
			set { buildVerbosityRegistryRoot = value; }
		}
		/// <summary>
		/// Set to null to avoid writing to the output window
		/// </summary>
		/*internal, but public for FSharp.Project.dll*/ public IVsOutputWindowPane OutputWindowPane
		{
			get { return outputWindowPane; }
			set { outputWindowPane = value; }
		}

        internal TaskReporter TaskReporter
        {
            get { return taskReporter; }
            set { taskReporter = value; }
        }
		#endregion

		#region ctors
		/// <summary>
		/// Constructor.  Inititialize member data.
		/// </summary>
		internal IDEBuildLogger(IVsOutputWindowPane output, TaskProvider taskProvider, IVsHierarchy hierarchy)
		{
			if (taskProvider == null)
				throw new ArgumentNullException("taskProvider");
			if (hierarchy == null)
				throw new ArgumentNullException("hierarchy");

			this.taskProvider = taskProvider;
            this.taskReporter = null;
			this.outputWindowPane = output;
			this.hierarchy = hierarchy;
			IOleServiceProvider site;
			Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(hierarchy.GetSite(out site));
			this.serviceProvider = new ServiceProvider(site);
		}
		#endregion

		#region overridden methods
		/// <summary>
		/// Overridden from the Logger class.
		/// </summary>
		public override void Initialize(IEventSource eventSource)
		{
			if (null == eventSource)
			{
				throw new ArgumentNullException("eventSource");
			}
            // Note that the MSBuild logger thread does not have an exception handler, so
            // we swallow all exceptions (lest some random bad thing bring down the process).
            eventSource.BuildStarted += new BuildStartedEventHandler(BuildStartedHandler);
            eventSource.BuildFinished += new BuildFinishedEventHandler(BuildFinishedHandler);
            eventSource.ProjectStarted += new ProjectStartedEventHandler(ProjectStartedHandler);
            eventSource.ProjectFinished += new ProjectFinishedEventHandler(ProjectFinishedHandler);
            eventSource.TargetStarted += new TargetStartedEventHandler(TargetStartedHandler);
            eventSource.TargetFinished += new TargetFinishedEventHandler(TargetFinishedHandler);
            eventSource.TaskStarted += new TaskStartedEventHandler(TaskStartedHandler);
            eventSource.TaskFinished += new TaskFinishedEventHandler(TaskFinishedHandler);
            eventSource.CustomEventRaised += new CustomBuildEventHandler(CustomHandler);
            eventSource.ErrorRaised += new BuildErrorEventHandler(ErrorHandler);
            eventSource.WarningRaised += new BuildWarningEventHandler(WarningHandler);
            eventSource.MessageRaised += new BuildMessageEventHandler(MessageHandler);
        }
		#endregion

		#region event delegates
		/// <summary>
		/// This is the delegate for error events.
		/// </summary>
		private void ErrorHandler(object sender, BuildErrorEventArgs errorEvent)
		{
            try
            {
                AddToErrorList(
                    errorEvent,
                    errorEvent.Subcategory,
                    errorEvent.Code,
                    errorEvent.File,
                    errorEvent.LineNumber,
                    errorEvent.ColumnNumber,
                    errorEvent.EndLineNumber,
                    errorEvent.EndColumnNumber);

            }
            catch (Exception e)
            {
                Debug.Assert(false, "Problem adding error to error list: " + e.Message + " at " + e.TargetSite);
            }
		}

		/// <summary>
		/// This is the delegate for warning events.
		/// </summary>
		private void WarningHandler(object sender, BuildWarningEventArgs errorEvent)
		{
            try
            {
                AddToErrorList(
                    errorEvent,
                    errorEvent.Subcategory, 
                    errorEvent.Code,
                    errorEvent.File,
                    errorEvent.LineNumber,
                    errorEvent.ColumnNumber,
                    errorEvent.EndLineNumber,
                    errorEvent.EndColumnNumber);
            }
            catch (Exception e)
            {
                Debug.Assert(false, "Problem adding warning to warning list: " + e.Message + " at " + e.TargetSite);
            }
		}
        /// <summary>
        /// Private internal class for capturing full compiler error line/column span information
        /// </summary>
        private class DefaultCompilerError : CompilerError
        {
            private int endLine;
            private int endColumn;

            public int EndLine
            {
                get { return endLine; }
                set { endLine = value; }
            }

            public int EndColumn
            {
                get { return endColumn; }
                set { endColumn = value; }
            }

            public DefaultCompilerError(string fileName,
                int startLine,
                int startColumn,
                int endLine,
                int endColumn,
                string errorNumber,
                string errorText)
                : base(fileName, startLine, startColumn, errorNumber, errorText)
            {
                EndLine = endLine;
                EndColumn = endColumn;
            }
        }

        private void Output(string s)
        {
            // Various events can call SetOutputLogger(null), which will null out "this.OutputWindowPane".  
            // So we capture a reference to it.  At some point in the future, after this build finishes, 
            // the pane reference we have will no longer accept input from us.
            // But here there is no race, because
            //  - we only log user-invoked builds to the Output window
            //  - user-invoked buils always run MSBuild ASYNC
            //  - in an ASYNC build, the BuildCoda uses UIThread.Run() to schedule itself to be run on the UI thread
            //  - UIThread.Run() protects against re-entrancy and thus always preserves the queuing order of its actions
            //  - the pane is good until at least the point when BuildCoda runs and we declare to MSBuild that we are finished with this build
            var pane = this.OutputWindowPane;  // copy to capture in delegate
            UIThread.Run(delegate()
            {
                try
                {
                    pane.OutputStringThreadSafe(s);
                }
                catch (Exception e)
                {
                    Debug.Assert(false, "We would like to know if this happens; exception in IDEBuildLogger.Output(): " + e.ToString());
                    // Don't crash process due to random exception, just swallow it
                }
            });
        }
 
		/// <summary>
		/// Add the error/warning to the error list and potentially to the output window.
		/// </summary>
		private void AddToErrorList(
			BuildEventArgs errorEvent,
            string subcategory,
			string errorCode,
			string file,
			int startLine,
			int startColumn,
            int endLine,
            int endColumn)
		{
            if (String.IsNullOrEmpty(file))
                return;
            
            bool isWarning = errorEvent is BuildWarningEventArgs;
            TaskPriority priority = isWarning ? TaskPriority.Normal : TaskPriority.High;
            
            TextSpan span;
            span.iStartLine = startLine;
            span.iStartIndex = startColumn;
            span.iEndLine = endLine < startLine ? span.iStartLine : endLine;
            span.iEndIndex = (endColumn < startColumn) && (span.iStartLine == span.iEndLine) ? span.iStartIndex : endColumn;

			if (OutputWindowPane != null
				&& (this.Verbosity != LoggerVerbosity.Quiet || errorEvent is BuildErrorEventArgs))
			{
				// Format error and output it to the output window
				string message = this.FormatMessage(errorEvent.Message);
                DefaultCompilerError e = new DefaultCompilerError(file,
                                                span.iStartLine,
                                                span.iStartIndex,
                                                span.iEndLine,
                                                span.iEndIndex,
                                                errorCode,
					                            message);
				e.IsWarning = isWarning;

				Output(GetFormattedErrorMessage(e));
			}

            UIThread.Run(delegate()
            {
                IVsUIShellOpenDocument openDoc = serviceProvider.GetService(typeof(IVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
                if (openDoc == null)
                    return;

                IVsWindowFrame frame;
                IOleServiceProvider sp;
                IVsUIHierarchy hier;
                uint itemid;
                Guid logicalView = VSConstants.LOGVIEWID_Code;

                IVsTextLines buffer = null;
                // JAF 
                // Notes about acquiring the buffer:
                // If the file physically exists then this will open the document in the current project. It doesn't matter if the file is a member of the project.
                // Also, it doesn't matter if this is an F# file. For example, an error in Microsoft.Common.targets will cause a file to be opened here.
                // However, opening the document does not mean it will be shown in VS. 
                if (!Microsoft.VisualStudio.ErrorHandler.Failed(openDoc.OpenDocumentViaProject(file, ref logicalView, out sp, out hier, out itemid, out frame)) && frame != null) {
                    object docData;
                    frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocData, out docData);

                    // Get the text lines
                    buffer = docData as IVsTextLines;

                    if (buffer == null) {
                        IVsTextBufferProvider bufferProvider = docData as IVsTextBufferProvider;
                        if (bufferProvider != null) {
                            bufferProvider.GetTextBuffer(out buffer);
                        }
                    }
                }

                // Need to adjust line and column indexing for the task window, which assumes zero-based values
                if (span.iStartLine > 0 && span.iStartIndex > 0)
                {
                    span.iStartLine -= 1;
                    span.iEndLine -= 1;
                    span.iStartIndex -= 1;
                    span.iEndIndex -= 1;
                }

                // Add new document task to task list
                DocumentTask task = new DocumentTask(serviceProvider,
                    buffer, // May be null
                    // This seems weird. Why would warning status make this a 'compile error'? 
                    // The �code sense� errors produce red squiggles, whereas the �compile� errors produce blue squiggles.  (This is in line with C#�s pre-VS2008-SP1 behavior.)  Swapping these two gives us a look consistent with that of the language service.
                    isWarning ? MARKERTYPE.MARKER_COMPILE_ERROR : MARKERTYPE.MARKER_CODESENSE_ERROR, 
                    span,
                    file,
                    subcategory);

                // Add error to task list
                task.Text = Microsoft.FSharp.Compiler.ErrorLogger.NewlineifyErrorString(errorEvent.Message);
                task.Priority = priority;
                task.ErrorCategory = isWarning ? TaskErrorCategory.Warning : TaskErrorCategory.Error;
                task.Category = TaskCategory.BuildCompile;
                task.HierarchyItem = hierarchy;
                task.Navigate += new EventHandler(NavigateTo);

                if (null != this.TaskReporter)
                {
                    this.taskReporter.AddTask(task);
                }
                else
                {
                    this.taskProvider.Tasks.Add(task);
                }
            });
		}


		/// <summary>
		/// This is the delegate for Message event types
		/// </summary>		
		private void MessageHandler(object sender, BuildMessageEventArgs messageEvent)
		{
            try
            {
                if (LogAtImportance(messageEvent.Importance))
                {
                    LogEvent(sender, messageEvent);
                }
            }
            catch (Exception e)
            {
                Debug.Assert(false, "Problem logging message event: " + e.Message + " at " + e.TargetSite);
                // swallow the exception
            }
		}

		private void NavigateTo(object sender, EventArgs arguments)
		{
            try {
                Microsoft.VisualStudio.Shell.Task task = sender as Microsoft.VisualStudio.Shell.Task;
                if (task == null)
                    throw new ArgumentException("sender");

                // Get the doc data for the task's document
                if (String.IsNullOrEmpty(task.Document))
                    return;

                IVsUIShellOpenDocument openDoc = serviceProvider.GetService(typeof(IVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
                if (openDoc == null)
                    return;

                IVsWindowFrame frame;
                IOleServiceProvider sp;
                IVsUIHierarchy hier;
                uint itemid;
                Guid logicalView = VSConstants.LOGVIEWID_Code;

                if (Microsoft.VisualStudio.ErrorHandler.Failed(openDoc.OpenDocumentViaProject(task.Document, ref logicalView, out sp, out hier, out itemid, out frame)) || frame == null)
                    return;

                object docData;
                frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocData, out docData);

                // Get the VsTextBuffer
                VsTextBuffer buffer = docData as VsTextBuffer;
                if (buffer == null) {
                    IVsTextBufferProvider bufferProvider = docData as IVsTextBufferProvider;
                    if (bufferProvider != null) {
                        IVsTextLines lines;
                        Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(bufferProvider.GetTextBuffer(out lines));
                        buffer = lines as VsTextBuffer;
                        Debug.Assert(buffer != null, "IVsTextLines does not implement IVsTextBuffer");
                        if (buffer == null)
                            return;
                    }
                }

                // Finally, perform the navigation.
                IVsTextManager mgr = serviceProvider.GetService(typeof(VsTextManagerClass)) as IVsTextManager;
                if (mgr == null)
                    return;

                // We should use the full span information if we've been given a DocumentTask
                bool isDocumentTask = task is DocumentTask;
                int endLine = isDocumentTask ? ((DocumentTask)task).Span.iEndLine : task.Line;
                int endColumn = isDocumentTask ? ((DocumentTask)task).Span.iEndIndex : task.Column;

                mgr.NavigateToLineAndColumn(buffer, ref logicalView, task.Line, task.Column, endLine, endColumn);
            } catch (Exception e) {
                System.Diagnostics.Debug.Assert(false, "Error thrown from NavigateTo. " + e.ToString());
            }
		}

		/// <summary>
		/// This is the delegate for BuildStartedHandler events.
		/// </summary>
		private void BuildStartedHandler(object sender, BuildStartedEventArgs buildEvent)
		{
            try
            {
                this.haveCachedRegistry = false;
                if (LogAtImportance(MessageImportance.Low))
                {
                    LogEvent(sender, buildEvent);
                }       	
            }
            catch (Exception e)
            {
                Debug.Assert(false, "Problem logging buildstarted event: " + e.Message + " at " + e.TargetSite);
                // swallow the exception
            }
            finally
            {
                // remove all Project System tasks from the task list, add everything else to the task set
                taskReporter.ClearAllTasks();
            }
		}

		/// <summary>
		/// This is the delegate for BuildFinishedHandler events.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="buildEvent"></param>
		private void BuildFinishedHandler(object sender, BuildFinishedEventArgs buildEvent)
		{
            try
            {
                if (LogAtImportance(buildEvent.Succeeded ? MessageImportance.Low :
                                                           MessageImportance.High))
                {
                    if (this.outputWindowPane != null)
                        Output(Environment.NewLine);
                    LogEvent(sender, buildEvent);
                }
                // BRIANMCN:
                // There are two reasons to call UIThread.Run.  
                // The obvious one is when you have to call IVsBlahBlah that must be accessed on the UI thread.  
                // That�s not the case here, here it�s for the less obvious reason that, whereas all the events 
                // happening in this class (that happen on the MSBuild logger thread) have a chronological 
                // ordering, most of those events transfer to the UIThread via UIThread.Run, and so we need to 
                // preserve the ordering.
                UIThread.Run(delegate()
                    {
                        taskReporter.OutputTaskList();
                    });
            }
            catch (Exception e)
            {
                Debug.Assert(false, "Problem logging buildfinished event: " + e.Message + " at " + e.TargetSite);
                // swallow the exception
            }
		}


		/// <summary>
		/// This is the delegate for ProjectStartedHandler events.
		/// </summary>
		private void ProjectStartedHandler(object sender, ProjectStartedEventArgs buildEvent)
		{
            try
            {
                if (LogAtImportance(MessageImportance.Low))
                {
                    LogEvent(sender, buildEvent);
                }
            }
            catch (Exception e)
            {
                Debug.Assert(false, "Problem logging projectstarted event: " + e.Message + " at " + e.TargetSite);
                // swallow the exception
            }
		}

		/// <summary>
		/// This is the delegate for ProjectFinishedHandler events.
		/// </summary>
		private void ProjectFinishedHandler(object sender, ProjectFinishedEventArgs buildEvent)
		{
            try
            {
                if (LogAtImportance(buildEvent.Succeeded ? MessageImportance.Low
                                                         : MessageImportance.High))
                {
                    LogEvent(sender, buildEvent);
                }
            }
            catch (Exception e)
            {
                Debug.Assert(false, "Problem logging projectfinished event: " + e.Message + " at " + e.TargetSite);
                // swallow the exception
            }
		}

		/// <summary>
		/// This is the delegate for TargetStartedHandler events.
		/// </summary>
		private void TargetStartedHandler(object sender, TargetStartedEventArgs buildEvent)
		{
            try
            {
                if (LogAtImportance(MessageImportance.Normal))
                {
                    LogEvent(sender, buildEvent);
                }
            }
            catch (Exception e)
            {
                Debug.Assert(false, "Problem logging targetstarted event: " + e.Message + " at " + e.TargetSite);
                // swallow the exception
            }
            finally
            {
                ++this.currentIndent;
            }
		}


		/// <summary>
		/// This is the delegate for TargetFinishedHandler events.
		/// </summary>
		private void TargetFinishedHandler(object sender, TargetFinishedEventArgs buildEvent)
		{
            try
            {
                --this.currentIndent;
                if ((isLogTaskDone) &&
                    LogAtImportance(buildEvent.Succeeded ? MessageImportance.Low
                                                         : MessageImportance.High))
                {
                    LogEvent(sender, buildEvent);
                }
            }
            catch (Exception e)
            {
                Debug.Assert(false, "Problem logging targetfinished event: " + e.Message + " at " + e.TargetSite);
                // swallow the exception
            }
		}


		/// <summary>
		/// This is the delegate for TaskStartedHandler events.
		/// </summary>
		private void TaskStartedHandler(object sender, TaskStartedEventArgs buildEvent)
		{
            try
            {
                if (LogAtImportance(MessageImportance.Normal))
                {
                    LogEvent(sender, buildEvent);
                }
            }
            catch (Exception e)
            {
                Debug.Assert(false, "Problem logging taskstarted event: " + e.Message + " at " + e.TargetSite);
                // swallow the exception
            }
            finally
            {
                ++this.currentIndent;
            }
		}


		/// <summary>
		/// This is the delegate for TaskFinishedHandler events.
		/// </summary>
		private void TaskFinishedHandler(object sender, TaskFinishedEventArgs buildEvent)
		{
            try
            {
                --this.currentIndent;
                if ((isLogTaskDone) &&
                    LogAtImportance(buildEvent.Succeeded ? MessageImportance.Normal
                                                         : MessageImportance.High))
                {
                    LogEvent(sender, buildEvent);
                }
            }
            catch (Exception e)
            {
                Debug.Assert(false, "Problem logging taskfinished event: " + e.Message + " at " + e.TargetSite);
                // swallow the exception
            }
		}


		/// <summary>
		/// This is the delegate for CustomHandler events.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="buildEvent"></param>
		private void CustomHandler(object sender, CustomBuildEventArgs buildEvent)
		{
            try
            {
                LogEvent(sender, buildEvent);
            }
            catch (Exception e)
            {
                Debug.Assert(false, "Problem logging custom event: " + e.Message + " at " + e.TargetSite);
                // swallow the exception
            }
		}

		#endregion

		#region helpers
		/// <summary>
		/// This method takes a MessageImportance and returns true if messages
		/// at importance i should be loggeed.  Otherwise return false.
		/// </summary>
		private bool LogAtImportance(MessageImportance importance)
		{
			// If importance is too low for current settings, ignore the event
			bool logIt = false;

			this.SetVerbosity();

			switch (this.Verbosity)
			{
				case LoggerVerbosity.Quiet:
					logIt = false;
					break;
				case LoggerVerbosity.Minimal:
					logIt = (importance == MessageImportance.High);
					break;
				case LoggerVerbosity.Normal:
				// Falling through...
				case LoggerVerbosity.Detailed:
					logIt = (importance != MessageImportance.Low);
					break;
				case LoggerVerbosity.Diagnostic:
					logIt = true;
					break;
				default:
					Debug.Fail("Unknown Verbosity level. Ignoring will cause everything to be logged");
					break;
			}

			return logIt;
		}

		/// <summary>
		/// This is the method that does the main work of logging an event
		/// when one is sent to this logger.
		/// </summary>
		private void LogEvent(object sender, BuildEventArgs buildEvent)
		{
            try
            {
                // Fill in the Message text
                if (OutputWindowPane != null && !String.IsNullOrEmpty(buildEvent.Message))
                {
                    int startingSize = this.currentIndent + buildEvent.Message.Length + 1;
                    StringBuilder msg = new StringBuilder(startingSize);
                    if (this.currentIndent > 0)
                    {
                        msg.Append('\t', this.currentIndent);
                    }
                    msg.AppendLine(buildEvent.Message);
                    Output(msg.ToString());
                }
            }
            catch (Exception e)
            {
                try
                {
                    System.Diagnostics.Debug.Assert(false, "Error thrown from IDEBuildLogger::LogEvent");
                    System.Diagnostics.Debug.Assert(false, e.ToString());
                    // For retail, also try to show in the output window.
                    Output(e.ToString());
                }
                catch 
                { 
                    // We're going to throw the original exception anyway
                }
                throw;
            }
		}

		/// <summary>
		/// This is called when the build complete.
		/// </summary>
		private void ShutdownLogger()
		{
		}


		/// <summary>
		/// Format error messages for the task list
		/// </summary>
		/// <param name="e"></param>
		/// <returns></returns>
        private string GetFormattedErrorMessage(DefaultCompilerError e)
		{
			if (e == null) return String.Empty;

			string errCode = (e.IsWarning) ? this.warningString : this.errorString;
			StringBuilder fileRef = new StringBuilder();

            // JAF:
            // Even if Fsc.exe returns a canonical message with no file at all, MSBuild will set the file to the name 
            // of the task (FSC). In principle, e.FileName will not be null or empty but handle this case anyway. 
            bool thereIsAFile = !string.IsNullOrEmpty(e.FileName);
            bool thereIsASpan = e.Line!=0 || e.Column!=0 || e.EndLine!=0 || e.EndColumn!=0;
            if (thereIsAFile) {
                fileRef.AppendFormat(CultureInfo.CurrentUICulture, "{0}", e.FileName);
                if (thereIsASpan) {
                    fileRef.AppendFormat(CultureInfo.CurrentUICulture, "({0},{1})", e.Line, e.Column);
                }
                fileRef.AppendFormat(CultureInfo.CurrentUICulture, ":");
            }

            fileRef.AppendFormat(CultureInfo.CurrentUICulture, " {0} {1}: {2}", errCode, e.ErrorNumber, e.ErrorText);
            return fileRef.ToString();
		}

		/// <summary>
		/// Formats the message that is to be output.
		/// </summary>
		/// <param name="message">The message string.</param>
		/// <returns>The new message</returns>
		private string FormatMessage(string message)
		{
			if (string.IsNullOrEmpty(message))
			{
				return Environment.NewLine;
			}

			StringBuilder sb = new StringBuilder(message.Length + Environment.NewLine.Length);

			sb.AppendLine(message);
			return sb.ToString();
		}

		/// <summary>
		/// Sets the verbosity level.
		/// </summary>
		private void SetVerbosity()
		{
			// TODO: This should be replaced when we have a version that supports automation.
            if (!this.haveCachedRegistry)
            {
                string verbosityKey = String.Format(CultureInfo.InvariantCulture, @"{0}\{1}", BuildVerbosityRegistryRoot, buildVerbosityRegistrySubKey);
                using (RegistryKey subKey = Registry.CurrentUser.OpenSubKey(verbosityKey))
                {
                    if (subKey != null)
                    {
                        object valueAsObject = subKey.GetValue(buildVerbosityRegistryKey);
                        if (valueAsObject != null)
                        {
                            this.Verbosity = (LoggerVerbosity)((int)valueAsObject);
                        }
                    }
                }
                this.haveCachedRegistry = true;
            }

			// TODO: Continue this code to get the Verbosity when we have a version that supports automation to get the Verbosity.
			//EnvDTE.DTE dte = this.serviceProvider.GetService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
			//EnvDTE.Properties properties = dte.get_Properties(EnvironmentCategory, ProjectsAndSolutionSubCategory);
		}
		#endregion
	}

}

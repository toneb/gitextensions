﻿using System.Diagnostics;
using Microsoft.VisualStudio.Threading;

namespace GitUI
{
    public class TaskManager
    {
        private readonly JoinableTaskCollection _joinableTaskCollection;

        public TaskManager(JoinableTaskContext joinableTaskContext)
        {
            JoinableTaskContext = joinableTaskContext;
            _joinableTaskCollection = joinableTaskContext.CreateCollection();
            JoinableTaskFactory = joinableTaskContext.CreateFactory(_joinableTaskCollection);
        }

        public JoinableTaskContext JoinableTaskContext { get; init; }

        public JoinableTaskFactory JoinableTaskFactory { get; init; }

        /// <summary>
        /// Handle all exceptions from asynchronous execution of <paramref name="asyncAction"/> by calling <paramref name="handleExceptionAsync"/> except for <see cref="OperationCanceledException"/>, which is ignored.
        /// </summary>
        internal static async Task HandleExceptionsAsync(Func<Task> asyncAction, Func<Exception, Task> handleExceptionAsync)
        {
            try
            {
                await asyncAction();
            }
            catch (OperationCanceledException)
            {
                // Do not rethrow these
            }
            catch (Exception ex)
            {
                await handleExceptionAsync(ex);
            }
        }

        /// <summary>
        /// Handle all exceptions from synchronous execution of <paramref name="action"/> by calling <paramref name="handleException"/> except for <see cref="OperationCanceledException"/>, which is ignored.
        /// </summary>
        public static void HandleExceptions(Action action, Action<Exception> handleException)
        {
            try
            {
                action();
            }
            catch (OperationCanceledException)
            {
                // Do not rethrow these
            }
            catch (Exception ex)
            {
                handleException(ex);
            }
        }

        /// <summary>
        /// Asynchronously run <paramref name="asyncAction"/> on a background thread and forward all exceptions to <see cref="UiApplication.OnThreadException"/> except for <see cref="OperationCanceledException"/>, which is ignored.
        /// </summary>
        public void FileAndForget(Func<Task> asyncAction)
        {
            JoinableTaskFactory.RunAsync(async () =>
                {
                    await TaskScheduler.Default;
                    await HandleExceptionsAsync(asyncAction, ReportExceptionOnMainThreadAsync);
                });
        }

        /// <summary>
        /// Asynchronously run <paramref name="action"/> on a background thread and forward all exceptions to <see cref="UiApplication.OnThreadException"/> except for <see cref="OperationCanceledException"/>, which is ignored.
        /// </summary>
        public void FileAndForget(Action action)
        {
            FileAndForget(() =>
                {
                    action();
                    return Task.CompletedTask;
                });
        }

        /// <summary>
        /// Asynchronously run <paramref name="task"/> on a background thread and forward all exceptions to <see cref="UiApplication.OnThreadException"/> except for <see cref="OperationCanceledException"/>, which is ignored.
        /// </summary>
        public void FileAndForget(Task task)
        {
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
            FileAndForget(async () => await task);
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks
        }

        public async Task JoinPendingOperationsAsync(CancellationToken cancellationToken)
        {
            await _joinableTaskCollection.JoinTillEmptyAsync(cancellationToken);
        }

        public void JoinPendingOperations()
        {
            // Note that JoinableTaskContext.Factory must be used to bypass the default behavior of JoinableTaskFactory
            // since the latter adds new tasks to the collection and would therefore never complete.
            JoinableTaskContext.Factory.Run(_joinableTaskCollection.JoinTillEmptyAsync);
        }

        /// <summary>
        /// Forward the exception <paramref name="ex"/> to <see cref="UiApplication.OnThreadException"/> on the main thread.
        /// </summary>
        /// The readability of the callstack is improved by calling <see cref="ExceptionExtensions.Demystify"/>.
        internal async Task ReportExceptionOnMainThreadAsync(Exception ex)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();
            UiApplication.OnThreadException(ex.Demystify());
        }
    }
}

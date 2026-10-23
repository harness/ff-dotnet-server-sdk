using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace io.harness.cfsdk.client
{
    /// <summary>
    /// Provides extension methods for running tasks.
    /// </summary>
    internal static class TaskExtensions
    {
        /// <summary>
        /// Runs a task with a specified timeout. If the task does not complete within the timeout, a <see cref="TimeoutException"/> is thrown.
        /// </summary>
        /// <typeparam name="TValue">The type of the value returned by the task.</typeparam>
        /// <param name="task">The task to run.</param>
        /// <param name="timeout">The timeout.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The value returned by the task.</returns>
        internal static async Task<TValue> RunWithTimeout<TValue>(this Task<TValue> task, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            await ((Task)task).RunWithTimeout(timeout, cancellationToken);
            return await task;
        }

        /// <summary>
        /// Runs a task with a specified timeout. If the task does not complete within the timeout, a <see cref="TimeoutException"/> is thrown.
        /// </summary>
        /// <param name="task">The task to run.</param>
        /// <param name="timeout">The timeout.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        internal static async Task RunWithTimeout(this Task task, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            var tasks = new List<Task>();
            if (task != null)
            {
                tasks.Add(task);
            }

            await RunWithTimeout(tasks, timeout, cancellationToken);
        }

        /// <summary>
        /// Runs the collection of tasks with a specified timeout. If the tasks do not complete within the timeout, a <see cref="TimeoutException"/> is thrown.
        /// </summary>
        /// <param name="task">The tasks to run.</param>
        /// <param name="timeout">The timeout.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        internal static async Task RunWithTimeout(this IEnumerable<Task> tasks, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            var allTasks = Task.WhenAll(tasks);

#if NET6_0_OR_GREATER
            await allTasks.WaitAsync(timeout, cancellationToken);
#else
            var timeoutTask = Task.Delay(timeout, cancellationToken);
            var completedTask = await Task.WhenAny(allTasks, timeoutTask);
            if (completedTask == timeoutTask)
            {
                throw new TimeoutException();
            }
#endif
        }
    }
}